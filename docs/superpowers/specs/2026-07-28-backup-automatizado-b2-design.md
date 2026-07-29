# Backup automatizado — Backblaze B2

**Data:** 2026-07-28
**Projetos afetados:** `ProximoTurnoApi` (BackgroundService, Dockerfile, configuração)

## Problema

Hoje não existe backup. Todo o estado do sistema vive em volumes Docker num único VPS:

| Dado | Volume | Existe em outro lugar? |
|---|---|---|
| Banco MySQL | `mysql_data` | Não — clientes, pedidos, jogos, contratos |
| Manuais enviados | `api_uploads` | Não — cópia única |
| Chaves Data Protection | `api_keys` | Não |
| Imagens dos jogos | — | Sim, Cloudinary |
| Contratos assinados | — | Sim, Autentique |

Se o VPS for perdido — falha de hardware, encerramento do provedor, suspensão de conta — o negócio perde a base de clientes e o histórico de pedidos por completo, sem caminho de recuperação.

## Cenário de recuperação alvo

**Perda total do VPS.** O objetivo é conseguir reconstruir a aplicação numa máquina nova a partir do repositório mais os backups.

**RPO: 24 horas.** Uma execução noturna. Perder até um dia de pedidos é recuperável manualmente; o custo de reduzir essa janela (envio de binlog, réplica) não se justifica na escala atual.

Explicitamente **não** é objetivo: recuperação pontual de registros apagados por engano, alta disponibilidade, nem retenção histórica para fins legais.

## Escopo

Entram no backup:

- **Banco MySQL** — dump completo, comprimido, cifrado, versionado a cada noite.
- **Volume `api_uploads`** — sincronização incremental, sem cifra.

Ficam de fora, por decisão:

- **`api_keys`** — recriável. O custo é que os tokens Bearer emitidos são invalidados e os usuários precisam fazer login de novo após uma restauração. Aceito.
- **`.env`** — os segredos ficam no gerenciador de senhas. Mantê-los fora do backup evita que credenciais de SMTP, Cloudinary e Autentique circulem junto com os dados.
- **Cloudinary e Autentique** — durabilidade de terceiros.

## Decisão de arquitetura

### Onde o backup roda: dentro da API

O backup é um `BackgroundService` no próprio `ProximoTurnoApi`, não um script com cron no host.

A alternativa (script shell + cron + serviço externo de monitoramento) foi descartada porque a API já tem tudo o que o backup precisa: SMTP configurado para alertas, o volume `api_uploads` montado, Serilog, `.env` e o projeto `Tests/`. Um script no host exigiria um segundo mecanismo de alerta e acesso ao volume Docker por fora.

O contêiner da API também já resolve as dependências: a imagem instala pacotes via `apt` (Dockerfile:28) e já traz `gnupg` (Dockerfile:32). Falta apenas `default-mysql-client`, uma linha no bloco existente.

O custo dessa escolha é o acoplamento: se a API não sobe, o backup não roda. Isso é aceitável porque a API indisponível é uma falha visível — é o sistema do negócio. O modo de falha realmente perigoso, a rotina parar em silêncio com a API saudável, é tratado pelo e-mail de sucesso descrito adiante.

### `mysqldump` conecta pela rede

`mysqldump` é cliente de rede: conecta em `proximoturno-mysql:3306` usando a connection string que a API já tem. Não é preciso `docker exec` nem montar o socket do Docker.

**O socket do Docker não deve ser montado no contêiner da API em hipótese alguma** — daria root no host a partir de um contêiner exposto à internet.

O dump usa `--single-transaction` para obter um snapshot consistente sem travar tabelas, de modo que o backup noturno não interrompa a API.

### Tratamento diferente para banco e uploads

Os dois conjuntos recebem tratamentos distintos porque têm naturezas distintas.

**Banco: cifrado e versionado.** `Cliente` guarda `Cpf`, `Email`, `Telefone` e `Endereco`, e o schema do Identity guarda hashes de senha. É um conjunto de dados pessoais sob a LGPD saindo do país para um provedor estrangeiro. A cifra é feita no VPS, antes do upload: a Backblaze armazena apenas um blob opaco. O dump é pequeno e muda todo dia, então uma cópia completa por noite é barata.

**Uploads: em claro e incremental.** Os arquivos em `api_uploads` são manuais de jogos, servidos publicamente sem autenticação em `/uploads/{guid}` (`Program.cs:163`). Cifrá-los no backup não protegeria nada. Como os nomes são GUIDs e nunca são sobrescritos, o diretório é imutável e cresce por adição — sincronizar apenas os arquivos novos mantém a execução noturna pequena, em vez de reenviar todos os manuais todas as noites.

### Retenção

Já está resolvida fora da aplicação: o bucket B2 está configurado com regra de ciclo de vida de **7 dias**. A aplicação nunca apaga objetos.

Cada noite grava um objeto novo em `db/AAAA-MM-DD.sql.gz.gpg` — o versionamento vem da data no nome, e a regra do bucket descarta os que passam de 7 dias. Não há separação em prefixos de diário e mensal.

Consequência assumida: não existe arquivo mensal, portanto não há restauração para um ponto de meses atrás. Coerente com o cenário de recuperação escolhido.

## Componentes

```
BackgroundService (PeriodicTimer + recuperação de execução perdida)
   │
   ├─ mysqldump --single-transaction → gzip → gpg → arquivo temporário → B2 (db/)
   ├─ varre /app/wwwroot/uploads → envia apenas chaves ausentes → B2 (uploads/)
   └─ SMTP: "Backup OK, 42 MB"  |  "Backup FALHOU: <erro>"
```

### `BackupService` (BackgroundService)

Orquestra a execução e é a única peça com dependência de tempo. Responsabilidades:

- Dispara uma vez por dia no horário configurado.
- Ao iniciar, verifica a data da última execução bem-sucedida; se passou mais de 24h, roda imediatamente. Isso evita que um deploy às 02:59 pule a noite em silêncio.
- Impede execuções simultâneas.
- Registra **data e tamanho do dump** da última execução bem-sucedida num arquivo JSON, para sobreviver a reinícios do contêiner. O tamanho alimenta a verificação descrita em Alertas.

O arquivo de estado vive num volume Docker novo e dedicado (`api_backup_state`), montado em `/app/backup-state`. Não é reaproveitado nenhum volume existente: `api_keys` tem outra finalidade e `api_uploads` é sincronizado para o bucket, o que enviaria o arquivo de estado junto com os manuais.
- Captura falhas de cada etapa e dispara o e-mail correspondente.

### `DumpBanco`

Executa `mysqldump` como processo filho, com a saída em pipe para `gzip` e em seguida para `gpg --batch --symmetric --cipher-algo AES256`, gravando em arquivo temporário. Trabalhar em disco, e não em memória, evita carregar o dump inteiro no processo da API e permite conferir o tamanho antes do envio.

Retorna o caminho e o tamanho do arquivo. O arquivo temporário é removido ao final, inclusive em caso de falha.

### `SincronizarUploads`

Lista os arquivos locais e as chaves já presentes no prefixo `uploads/` do bucket, e envia apenas a diferença. Não apaga nada remoto — a regra de 7 dias do bucket é quem governa.

### `ClienteB2`

Encapsula o `AWSSDK.S3` apontado para o endpoint S3-compatível da Backblaze (`ServiceURL` + `ForcePathStyle`). Expõe apenas o necessário: listar chaves de um prefixo e enviar um arquivo.

### `NotificadorBackup`

Envia o e-mail de resultado pela infraestrutura SMTP já existente.

## Alertas

**E-mail de sucesso todas as noites, não apenas de falha.**

Alerta só em falha não detecta o pior caso: a rotina nunca ter executado. Se nada roda, não há falha para reportar, e o backup pode ficar parado por meses sem sinal.

Invertendo a lógica, a ausência do e-mail passa a ser o próprio alarme, e a caixa de entrada faz o papel de monitor — sem serviço externo. O e-mail de sucesso traz data, tamanho do dump e quantidade de uploads novos.

**Verificação de tamanho.** Antes do envio, o tamanho do dump é comparado com o da última execução registrada no arquivo de estado. Uma redução superior a 50% aborta a execução e dispara o e-mail de falha. Isso pega o caso de um `mysqldump` que "funcionou" contra um banco vazio ou parcialmente migrado.

Na primeira execução não há tamanho anterior para comparar; a verificação é ignorada e o valor é apenas registrado.

## Configuração

As opções ficam numa classe `BackupOptions`, com valores padrão embutidos no código. O `.env` só precisa declarar o que foge do padrão — na prática, apenas os três segredos.

| Variável | Padrão embutido |
|---|---|
| `BACKUP_ENABLED` | `true` |
| `BACKUP_HORA` | `03:00` (fuso do contêiner já é `America/Sao_Paulo`) |
| `BACKUP_EMAIL_DESTINO` | `contato@proximoturno.com.br` |
| `B2_ENDPOINT` | `https://s3.us-east-005.backblazeb2.com` |
| `B2_BUCKET` | `proximo-turno` |
| `BACKUP_PASSPHRASE` | **sem padrão** |
| `B2_KEY_ID` | **sem padrão** |
| `B2_APPLICATION_KEY` | **sem padrão** |

Os três últimos são segredos e não têm valor embutido. Se qualquer um deles estiver ausente na inicialização, o serviço registra um erro no log e não agenda execuções — em vez de falhar toda noite às 03:00 e encher a caixa de entrada de e-mails de falha.

A `BACKUP_PASSPHRASE` é o único item cuja perda torna os backups inúteis. Deve ser guardada no gerenciador de senhas **e** numa cópia offline.

Como `BACKUP_ENABLED` vem `true` por padrão, o ambiente de desenvolvimento precisa de `BACKUP_ENABLED=false` no `.env` local. Sem isso o comportamento não é perigoso — os segredos não existem na máquina de desenvolvimento, então o serviço apenas se recusa a agendar —, mas deixar explícito evita ruído no log.

Alteração no `Dockerfile`: acrescentar `default-mysql-client` ao bloco `apt-get install` existente.

## Restauração

A restauração fica num `docs/RESTORE.md` versionado, com comandos shell diretos — **não** dentro da API, porque no momento da restauração a API ainda não existe.

Sequência: provisionar máquina → clonar repositório → restaurar `.env` do gerenciador de senhas → subir apenas o MySQL → baixar o objeto do B2 → `gpg -d` → `gunzip` → `mysql <` → copiar os uploads de volta para o volume → subir a stack completa.

O texto é escrito para ser seguido sob pressão, sem depender de contexto na cabeça de quem executa.

**O procedimento precisa ser testado uma vez, contra um contêiner MySQL descartável, antes de considerar o trabalho concluído.** Backup sem restauração testada não é backup.

## Testes

`BackupService` recebe suas dependências por interface, de modo que a orquestração é testável sem tocar em MySQL, Backblaze ou SMTP. Casos cobertos no projeto `Tests/`:

- Execução bem-sucedida dispara o e-mail de sucesso.
- Falha no dump dispara o e-mail de falha e não tenta o envio.
- Dump com redução superior a 50% aborta e alerta.
- Última execução com mais de 24h dispara execução imediata ao iniciar.
- Última execução recente não dispara execução ao iniciar.
- Sincronização de uploads envia apenas as chaves ausentes.

O caminho real — `mysqldump`, GPG e Backblaze de verdade — é validado manualmente na primeira execução e pelo teste de restauração.
