# Integração Autentique — Design Spec

## Objetivo

Integrar a plataforma Próximo Turno com a API do Autentique para gerar e enviar contratos de aluguel para assinatura digital quando um pedido é finalizado. O cliente recebe um link direto de assinatura (sem envio de email) que pode ser aberto imediatamente em um modal no frontend ou consultado posteriormente.

## Decisões de Design

| Decisão | Escolha |
|---|---|
| Template do contrato | HTML com placeholders (`{{NOME_CLIENTE}}`, etc.), convertido para PDF |
| Quem assina | Apenas o cliente |
| Entrega do link | `DELIVERY_METHOD_LINK` — link direto retornado pela API |
| Acompanhamento | Webhook do Autentique (SIGNATURE_ACCEPTED) + rota de consulta manual |
| Envio por email | Não — apenas link direto |

---

## Arquitetura

```
┌─────────────────┐     POST /api/contratos/pedido/{id}     ┌──────────────────┐
│    Frontend     │ ──────────────────────────────────────► │  ContratosCtrl   │
│  (modal c/      │ ◄─────────── { signingLink }            │                  │
│   iframe)       │                                         └────────┬─────────┘
└─────────────────┘                                                  │
                                                          ┌─────────▼──────────┐
                                                          │ GerarContrato      │
                                                          │ UseCase            │
                                                          └──┬──────────┬──────┘
                                                             │          │
                                              ┌──────────────▼──┐  ┌───▼──────────────┐
                                              │ ContratoPdf     │  │ Autentique       │
                                              │ Service         │  │ Service          │
                                              │ (HTML→PDF)      │  │ (GraphQL client) │
                                              └─────────────────┘  └───┬──────────────┘
                                                                       │
                                                            ┌──────────▼──────────┐
                                                            │ api.autentique.com  │
                                                            │ /v2/graphql         │
                                                            └──────────┬──────────┘
                                                                       │ webhook
                                                            ┌──────────▼──────────┐
                                                            │ WebhooksController  │
                                                            │ POST /api/webhooks  │
                                                            │ /autentique         │
                                                            └─────────────────────┘
```

### Componentes Backend (foco)

1. **`AutentiqueService`** — Cliente HTTP para a API GraphQL do Autentique. Responsável por criar documentos (multipart upload), consultar status e obter/regenerar links de assinatura.

2. **`ContratoPdfService`** — Carrega o template HTML do contrato, substitui placeholders com dados do pedido/cliente, e converte para PDF usando PuppeteerSharp.

3. **`ContratoAutentique`** (model) — Tabela no banco para rastrear documentos enviados ao Autentique (document ID, public ID da assinatura, link, status).

4. **`ContratoRepository`** — Acesso a dados para a tabela CONTRATO_AUTENTIQUE.

5. **Use Cases:**
   - `GerarContratoPedido` — Orquestra: gera PDF → envia ao Autentique → salva no banco → retorna link
   - `ConsultarContratoPedido` — Consulta status/link de um contrato existente
   - `ProcessarWebhookAutentique` — Recebe eventos de webhook e atualiza status

6. **Controllers:**
   - `ContratosController` — Endpoints autenticados para gerar e consultar contratos
   - `WebhooksController` — Endpoint público para receber webhooks do Autentique

### Frontend (ideia geral — implementação futura)

- Após finalizar um pedido, o frontend chama `POST /api/contratos/pedido/{id}`
- Com o `signingLink` retornado, abre um modal contendo um iframe com a página de assinatura do Autentique
- Fallback: se o cliente não assinar na hora, pode consultar `GET /api/contratos/pedido/{id}` para obter o link novamente

---

## API do Autentique — Referência Rápida

| Item | Valor |
|---|---|
| Endpoint | `POST https://api.autentique.com.br/v2/graphql` |
| Auth | `Authorization: Bearer <TOKEN>` |
| Criar documento | `createDocument` mutation (multipart upload) |
| Obter link | `signatures.link.short_link` na resposta, ou `createLinkToSignature` mutation |
| Consultar status | `document(id: ...)` query |
| Webhooks | `createEndpoint` mutation ou config no dashboard |
| Rate limit | 60 req/min |
| Sandbox | `sandbox: true` no DocumentInput (não consome créditos) |

---

## Modelo de Dados

### Tabela `CONTRATO_AUTENTIQUE`

| Coluna | Tipo | Descrição |
|---|---|---|
| ID | int (PK, auto) | Identificador |
| ID_PEDIDO | int (FK → PEDIDO) | Pedido associado |
| AUTENTIQUE_DOCUMENT_ID | varchar(100) | ID do documento no Autentique |
| AUTENTIQUE_PUBLIC_ID | varchar(100) | Public ID da assinatura (para gerar links) |
| LINK_ASSINATURA | varchar(500) | Link curto de assinatura |
| STATUS | smallint | 0=Pendente, 1=Assinado, 2=Rejeitado |
| DATA_CRIACAO | datetime | Data de criação do contrato |
| DATA_ASSINATURA | datetime? | Data em que foi assinado |

---

## Placeholders do Template HTML

| Placeholder | Origem |
|---|---|
| `{{NUMERO_PEDIDO}}` | `Pedido.Id` |
| `{{DATA_PEDIDO}}` | `Pedido.DataHora` |
| `{{NOME_CLIENTE}}` | `Cliente.Nome` |
| `{{TELEFONE_CLIENTE}}` | `Cliente.Telefone` |
| `{{EMAIL_CLIENTE}}` | `Cliente.Email` |
| `{{ENDERECO_CLIENTE}}` | `Cliente.Endereco` |
| `{{TABELA_ITENS}}` | Gerado: linhas HTML com nome do jogo, valor, data devolução |
| `{{VALOR_TOTAL}}` | `Pedido.ValorTotal` |
| `{{VALOR_DESCONTO}}` | `Pedido.ValorDesconto` |
| `{{METODO_PAGAMENTO}}` | `Pedido.MetodoPagamento` |
| `{{METODO_ENTREGA}}` | `Pedido.MetodoEntrega` |
| `{{DATA_ATUAL}}` | `DateTime.Now` |

---

## Endpoints da API

| Método | Rota | Auth | Descrição |
|---|---|---|---|
| POST | `/api/contratos/pedido/{id}` | Bearer (Admin) | Gera contrato e envia ao Autentique |
| GET | `/api/contratos/pedido/{id}` | Bearer (Admin) | Consulta status/link do contrato |
| POST | `/api/webhooks/autentique` | Nenhuma (webhook secret via query param) | Recebe eventos do Autentique |

---

## Dependências Novas

| Pacote NuGet | Propósito |
|---|---|
| PuppeteerSharp | Conversão HTML → PDF |

---

## Configuração (.env)

```
AUTENTIQUE_API_TOKEN=seu_token_aqui
AUTENTIQUE_SANDBOX=true
AUTENTIQUE_WEBHOOK_SECRET=um_secret_aleatorio
```
