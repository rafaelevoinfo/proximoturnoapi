# Restauração do Próximo Turno

Procedimento para reconstruir a API numa máquina nova a partir dos backups na
Backblaze B2. Escrito para ser seguido sem depender de contexto prévio — se
você está lendo isto às 2h da manhã porque algo caiu, siga os passos na ordem,
não pule nenhum, e leia a caixa abaixo antes de tudo.

## ⚠️ Leia isto primeiro: status de verificação e retenção

**A retenção do bucket é de 7 dias.** Só existem os backups da última semana —
se o problema que te trouxe aqui tem mais de uma semana, não há backup dele.

**O que foi verificado, e por quem, antes deste runbook existir:**

- O ciclo cifra→decifra do GPG (`--symmetric --cipher-algo AES256`,
  passphrase por descritor de arquivo dedicado) foi verificado **pelo
  controlador da revisão da Task 8**, em isolamento (sem `mysqldump` real, sem
  upload/download real), com uma passphrase deliberadamente hostil (contendo
  aspas simples, `$` e crase): cifrou, decifrou, e o resultado foi
  **byte-idêntico** ao original. Isso confirma que o mecanismo de senha do
  pipeline de cifra está correto — foi exatamente aqui que a Task 8 pegou um
  defeito real (veja abaixo).
- **A cadeia completa nunca foi executada de ponta a ponta**: MySQL real → dump
  → compressão → cifra → upload para o B2 → download → decifra →
  descompressão → carga num MySQL → aplicação validando os dados. Nenhuma
  etapa dessa cadeia foi exercitada com todos os elos reais ao mesmo tempo.
- **O upload/download para a Backblaze B2 nunca rodou com credenciais reais.**
  O cliente S3 (`ArmazenamentoB2`) tem testes unitários com dublês, não uma
  chamada real contra o bucket `proximo-turno`.
- Este documento **não é o resultado de um ensaio de restauração**. Essa
  decisão foi tomada deliberadamente pelo parceiro humano ao encomendar esta
  task — não é um esquecimento, e este runbook não deve ser lido como "testado
  e aprovado".

**Por que isso importa:** a Task 8 introduziu, sem que nenhum teste ou build
pegasse, uma pipeline que saía com código 0 e gerava um arquivo `.gpg` de
aparência perfeitamente normal — mas que nenhuma senha jamais conseguiria
decifrar (`--passphrase-fd 0` numa pipe de 3 estágios lê os bytes do estágio
anterior, não o stdin do shell). O defeito só foi descoberto por revisão de
código, não por execução. Não há motivo para acreditar que essa é a última
classe de defeito desse tipo escondida no restante da cadeia — **e a única
forma de descobrir é executando a restauração de verdade.**

**Recomendação explícita: assim que o primeiro backup automático real for
gerado em produção, restaure-o contra um MySQL descartável (um container
`mysql:9.0` isolado, sem tocar em produção) antes de confiar neste sistema de
backup para valer.** Enquanto isso não for feito, trate este runbook como "os
passos que achamos que funcionam", não como "os passos que sabemos que
funcionam".

Ao longo do documento, comandos que dependem de elos nunca testados estão
marcados com **[não testado nesta forma]**.

## O que você precisa em mãos

- `BACKUP_PASSPHRASE` (gerenciador de senhas ou cópia offline — é o único item
  cuja perda torna os backups irrecuperáveis)
- `B2_KEY_ID` e `B2_APPLICATION_KEY`
- O conteúdo do `.env` de produção completo (não só os três segredos do
  backup — a aplicação inteira depende dele)
- Bucket: `proximo-turno`, endpoint `https://s3.us-east-005.backblazeb2.com`

## 1. Preparar a máquina

```bash
git clone <url-do-repositorio> ProximoTurnoApi && cd ProximoTurnoApi
```

**O nome desta pasta importa.** O Docker Compose deriva o prefixo dos volumes
nomeados (`api_uploads`, `api_keys`, `api_backup_state`, `mysql_data`) do nome
do diretório, em minúsculas (ex.: pasta `ProximoTurnoApi` → prefixo
`proximoturnoapi_`). Use o mesmo nome de pasta do passo 5 em diante, ou anote
o nome real assim que descobri-lo.

Restaure o `.env` de produção a partir do gerenciador de senhas nesta pasta.
Ele precisa conter, no mínimo: `MYSQL_ROOT_PASSWORD`, `MYSQL_DATABASE`,
`MYSQL_USER`, `MYSQL_PASSWORD`, `BACKUP_PASSPHRASE`, `B2_KEY_ID`,
`B2_APPLICATION_KEY`, e as demais variáveis da aplicação (`CLOUDINARY_URL`,
`SMTP_*`, `AUTENTIQUE_*`). Se alguma faltar, o `docker compose` sobe os
serviços com valores vazios silenciosamente (só avisa no log, não trava) —
confira com `docker compose config` antes de prosseguir se tiver dúvida.

**Se falhar:** sem o `.env` completo, o container `api` inicia mas falha ao
conectar em serviços externos (e-mail, Cloudinary, Autentique) de forma
pouco óbvia. Rode `docker compose config` e confira se algum valor apareceu
em branco antes de ir adiante.

## 2. Baixar o backup mais recente **[não testado nesta forma]**

Nunca foi feito um download real deste bucket com credenciais reais.

```bash
export AWS_ACCESS_KEY_ID=<B2_KEY_ID>
export AWS_SECRET_ACCESS_KEY=<B2_APPLICATION_KEY>
export ENDPOINT=https://s3.us-east-005.backblazeb2.com

# Listar o que existe
aws s3 ls s3://proximo-turno/db/ --endpoint-url $ENDPOINT

# Baixar (troque a data pela mais recente da listagem)
aws s3 cp s3://proximo-turno/db/2026-07-28.sql.gz.gpg . --endpoint-url $ENDPOINT
```

A chave segue o padrão `db/AAAA-MM-DD.sql.gz.gpg`, gerado por
`ExecutarBackup.cs` (`$"db/{DateTime.UtcNow:yyyy-MM-dd}.sql.gz.gpg"`).

**Se falhar:** a Backblaze B2 exige *path-style addressing* — o próprio
cliente S3 da aplicação força isso explicitamente
(`ForcePathStyle = true` em `ArmazenamentoB2.cs`) porque o endereçamento
virtual-host da AWS não funciona contra o endpoint da B2. Se o `aws s3 ls`
falhar com erro de resolução de host (tentando resolver
`proximo-turno.s3.us-east-005.backblazeb2.com`), force o addressing style:
`aws configure set default.s3.addressing_style path` e tente de novo. Se o
erro for de autenticação, confira se `B2_KEY_ID`/`B2_APPLICATION_KEY` não têm
espaço em branco colado do gerenciador de senhas.

## 3. Decifrar e descomprimir

O dump foi cifrado por `DumpBancoMySql.cs` com
`gpg --batch --yes --symmetric --cipher-algo AES256 --passphrase-fd 3`, senha
vinda da variável `BACKUP_PASSPHRASE`. A decifra usa o mesmo mecanismo de
senha do GPG (`--passphrase`), só que lendo o valor de uma variável de
ambiente em vez de digitado na hora — assim a senha real não fica exposta no
histórico do shell nem precisa ser escapada manualmente se tiver aspas ou
outros caracteres especiais.

```bash
# Carrega as variáveis do .env restaurado no passo 1 (já inclui BACKUP_PASSPHRASE)
set -a
source .env
set +a

# Decifra e descomprime num único passo
# Troque "2026-07-28.sql.gz.gpg" pelo nome exato do arquivo que você baixou
# no passo 2 — o nome de exemplo abaixo quase certamente não é o seu.
gpg --batch --yes --decrypt --passphrase "$BACKUP_PASSPHRASE" \
    2026-07-28.sql.gz.gpg | gunzip > backup.sql
```

Se o seu `.env` não estiver em formato compatível com `source` (uma variável
`CHAVE=valor` por linha), digite a senha sem que ela apareça na tela:

```bash
read -rs BACKUP_PASSPHRASE   # nada é exibido enquanto você digita
export BACKUP_PASSPHRASE
```

**Verificado:** este exato mecanismo de senha (`gpg --decrypt --passphrase`,
valor cifra→decifra) foi testado pelo controlador da Task 8 com uma senha
hostil (aspas simples, `$`, crase) e o resultado foi byte-idêntico ao
original — ver caixa no topo deste documento. A única diferença aqui é a
origem do valor (variável de ambiente em vez de literal na linha de comando),
o que é equivalente do ponto de vista do `gpg`.

**Se falhar com `gpg: decryption failed: Bad session key`:** confira se
`BACKUP_PASSPHRASE` no `.env` restaurado é exatamente a senha correta (sem
espaço a mais, sem quebra de linha). Se a senha estiver certa e mesmo assim
falhar, considere a hipótese de que este arquivo foi gerado antes da correção
do defeito da Task 8 (commit `8ca1b2b`) — antes dessa correção, a pipeline
gerava arquivos `.gpg` com aparência normal que **nenhuma senha decifra**.
Backups gerados depois da correção não deveriam ter esse problema, mas isso
nunca foi confirmado numa execução real de ponta a ponta.

## 4. Subir o banco e carregar o dump **[não testado nesta forma]**

```bash
docker compose up -d mysql
docker compose ps        # espere o status ficar "healthy"; pode levar alguns segundos

docker compose exec -T mysql mysql -u root -p"$MYSQL_ROOT_PASSWORD" "$MYSQL_DATABASE" < backup.sql
```

`docker compose up -d mysql` sobe **só** o serviço `mysql`, não seus
dependentes (`migrations`, `api`) — isso é intencional. **O dump já contém o
schema inteiro, então não rode `migrations` nem suba a aplicação antes desta
etapa.** Se `migrations` rodar antes do dump ser carregado, ele vai criar um
schema vazio que pode conflitar com o `CREATE TABLE` do dump.

**Se falhar com `ERROR 1045 Access denied`:** a senha em `$MYSQL_ROOT_PASSWORD`
não bate com a que o container `mysql` foi inicializado — confira o `.env`
contra o gerenciador de senhas.

**Se o import travar ou demorar muito sem dar erro:** dumps grandes podem
demorar; isso não é necessariamente uma falha. Se quiser acompanhar o
progresso, use `pv backup.sql | docker compose exec -T mysql mysql -u root -p"$MYSQL_ROOT_PASSWORD" "$MYSQL_DATABASE"`
no lugar do redirecionamento simples (requer `pv` instalado).

**Se aparecerem erros de chave estrangeira durante o import:** por padrão, o
`mysqldump` inclui os comandos que desabilitam a checagem de chave
estrangeira durante a carga e a reabilitam no final — comportamento esperado
da ferramenta, mas **não confirmado contra o dump real deste projeto**. Para
transformar essa suposição em fato antes que ela importe, confira você mesmo
em segundos: `grep FOREIGN_KEY_CHECKS backup.sql` deve mostrar as duas linhas
(desabilita no início, reabilita no fim). Se não mostrar, um erro de FK aqui é
mais provavelmente sinal de dump truncado (download incompleto no passo 2, ou
decifra que terminou antes da hora) do que de um problema real de dados.

## 5. Restaurar os uploads **[não testado nesta forma]**

Nunca foi testado com um bucket real nem com um volume real populado. A
ordem importa: o volume precisa existir e estar populado *antes* de subir a
aplicação, senão a API sobe servindo uma pasta de uploads vazia.

```bash
# Cria os containers (sem iniciá-los) só para o Compose materializar os
# volumes nomeados com o prefixo correto, sem subir mysql/migrations/api
docker compose create api

# Descubra o nome exato do volume (o prefixo depende do nome da pasta, passo 1)
docker volume ls --filter name=api_uploads --format '{{.Name}}'
# anote o resultado — vamos chamar de <VOLUME_UPLOADS> abaixo

aws s3 sync s3://proximo-turno/uploads/ ./uploads-restaurados/ --endpoint-url $ENDPOINT

docker run --rm \
  -v <VOLUME_UPLOADS>:/destino \
  -v "$(pwd)/uploads-restaurados":/origem \
  alpine sh -c "cp -a /origem/. /destino/"
```

**Se `docker volume ls` não retornar nada ou retornar mais de um resultado:**
confira se você não renomeou a pasta do projeto entre este passo e o passo 1
— o prefixo do volume vem do nome do diretório. Como alternativa, crie o
volume manualmente com o nome que você espera
(`docker volume create <pasta-em-minusculas>_api_uploads`) — o Compose só
cria o volume se ele ainda não existir, então isso é seguro de fazer antes.

**Se falhar:** confirme que `uploads-restaurados/` não ficou vazio depois do
`aws s3 sync` (bucket errado, prefixo errado, ou credenciais sem permissão de
leitura em `uploads/` dariam uma pasta vazia sem necessariamente um erro
visível).

## 6. Subir a aplicação

```bash
docker compose up -d
docker compose ps
```

Este `docker-compose.yml` **não publica a porta da API para o host** — só o
MySQL tem porta mapeada (`127.0.0.1:3308:3306`). O acesso externo em produção
passa por alguma camada fora deste repositório (proxy reverso, load
balancer). Confirme com quem administra a rede como a API é exposta antes de
tentar acessá-la de fora da máquina.

Para confirmar que a aplicação subiu saudável sem depender dessa camada
externa, use o healthcheck já definido no `docker-compose.yml`, de dentro do
próprio container:

```bash
docker compose ps                                    # STATUS deve mostrar "healthy"
docker compose exec api curl -f http://localhost/health   # alternativa direta
```

**Se o container `api` não ficar `healthy`:** veja `docker compose logs api`.
Causas prováveis: `.env` incompleto (passo 1), banco ainda não migrado
corretamente (confira `docker compose logs migrations` — deve ter terminado
com sucesso e sem tentar recriar tabelas que já vieram no dump).

## 7. Depois de restaurar

- **As chaves de Data Protection (`api_keys`) não são backupeadas, por
  decisão de projeto.** Elas são recriadas do zero nesta máquina nova, então
  **todos os tokens Bearer emitidos antes da restauração ficam inválidos** —
  todo usuário precisa fazer login de novo. Isso é esperado, não é um sinal
  de que algo deu errado.
- Confira se `BACKUP_ENABLED` no `.env` restaurado está em `true` (ou ausente
  — o padrão já é `true`). Se estiver `false` porque o `.env` veio de um
  ambiente de desenvolvimento por engano, os próximos backups não vão rodar.
- Confira se o backup seguinte roda normalmente: o e-mail "Backup OK" (ver
  `ExecutarBackup.cs`) deve chegar em `BACKUP_EMAIL_DESTINO` (padrão
  `contato@proximoturno.com.br`) na manhã seguinte à primeira noite rodando
  nesta máquina.
- **Antes de confiar de verdade neste sistema:** siga a recomendação do topo
  deste documento — restaure o primeiro backup real gerado por esta máquina
  contra um MySQL descartável. É a única forma de pegar, antes de precisar de
  verdade, um defeito da mesma classe do que a Task 8 já produziu uma vez.
