# Docker Setup para ProximoTurnoApi

Este projeto inclui configuração completa de Docker para facilitar o desenvolvimento e deployment.

## Arquivos incluídos

- **Dockerfile**: Build multi-stage otimizado para ASP.NET Core 8.0
- **docker-compose.yml**: Orquestração de containers (API + MySQL)
- **.dockerignore**: Otimização de build, ignorando arquivos desnecessários
- **.env.example**: Template de variáveis de ambiente

## Pré-requisitos

- Docker Desktop instalado (versão 20.10+)
- Docker Compose (geralmente incluído no Docker Desktop)

## Como usar

### 1. Configurar variáveis de ambiente

Copie o arquivo `.env.example` para `.env` e ajuste as credenciais conforme necessário:

```bash
cp .env.example .env
```

### 2. Executar com Docker Compose (Recomendado)

Esta é a forma mais fácil, pois inicia tanto a API quanto o MySQL automaticamente:

```bash
docker-compose up --build
```

- A API estará disponível em `http://localhost:8080`
- Swagger UI em `http://localhost:8080/swagger/index.html`
- MySQL estará acessível em `localhost:3306`

### 3. Build da imagem Docker manualmente

```bash
docker build -t proximoturno-api:latest .
```

### 4. Executar container manualmente

```bash
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Server=mysql;Port=3306;Database=proximoturno_db;User=proximoturno;Password=password123;" \
  proximoturno-api:latest
```

## Parar containers

```bash
docker-compose down
```

Para remover volumes de dados também:

```bash
docker-compose down -v
```

## Variáveis de ambiente importantes

- `ConnectionStrings__DefaultConnection`: String de conexão com MySQL
- `ASPNETCORE_ENVIRONMENT`: Ambiente da aplicação (Development/Production)
- `MYSQL_ROOT_PASSWORD`: Senha do root do MySQL
- `MYSQL_USER`: Usuário do MySQL
- `MYSQL_PASSWORD`: Senha do usuário MySQL

### Backup automatizado

Apenas os três segredos são obrigatórios — o resto tem padrão embutido no código:

- `BACKUP_PASSPHRASE`: senha da cifra GPG do dump. **Sem padrão.**
- `B2_KEY_ID` / `B2_APPLICATION_KEY`: credenciais da Backblaze B2. **Sem padrão.**

Se qualquer um dos três faltar, o serviço registra erro no log e não agenda execuções.

Opcionais, com padrão: `BACKUP_ENABLED` (`true`), `BACKUP_HORA` (`03:00`),
`BACKUP_EMAIL_DESTINO` (`contato@proximoturno.com.br`),
`B2_ENDPOINT` (`https://s3.us-east-005.backblazeb2.com`), `B2_BUCKET` (`proximo-turno`).

**Em desenvolvimento, coloque `BACKUP_ENABLED=false` no `.env` local.** Sem isso o
serviço tenta iniciar, não encontra os segredos e polui o log com erro a cada
inicialização.

A `BACKUP_PASSPHRASE` é o único item cuja perda torna os backups irrecuperáveis:
guarde no gerenciador de senhas **e** numa cópia offline.

## Troubleshooting

### Container da API não consegue conectar ao MySQL

1. Certifique-se de que o MySQL iniciou corretamente:
   ```bash
   docker-compose logs mysql
   ```

2. Aguarde o health check do MySQL passar antes de iniciar a API

### Porta já em uso

Se a porta 8080 ou 3306 já estão em uso, você pode mudar no `docker-compose.yml`:

```yaml
api:
  ports:
    - "8081:8080"  # Use 8081 localmente, expõe 8080 no container
```

### Resetar dados do banco

Para limpar completamente e começar do zero:

```bash
docker-compose down -v
docker-compose up --build
```

## Migrations do Entity Framework

As migrations devem ser executadas dentro do container. Para criar novas migrations:

```bash
docker-compose exec api dotnet ef migrations add NomeDaMigracao --project Src/ProximoTurnoApi.csproj
```

Para atualizar o banco:

```bash
docker-compose exec api dotnet ef database update --project Src/ProximoTurnoApi.csproj
```
