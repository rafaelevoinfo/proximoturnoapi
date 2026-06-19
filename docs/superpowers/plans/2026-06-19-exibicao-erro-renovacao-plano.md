# Exibição de Erros do Backend na Renovação de Pedidos - Plano de Implementação

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fazer com que o frontend extraia e exiba a mensagem de erro específica do backend em caso de erro na renovação de pedidos.

**Architecture:** Adicionar uma função utilitária de extração de erro `getErrorMessage` em `lib/api-service.ts` e usá-la nos blocos catch de renovação em `app/pedidos/page.tsx`.

**Tech Stack:** Next.js 16, React 19

---

### Task 1: Função Utilitária `getErrorMessage`

**Files:**
- Modify: `ProximoTurno/lib/api-service.ts`

- [ ] **Step 1: Adicionar exportação de `getErrorMessage`**
  No final do arquivo `lib/api-service.ts`, adicionar a definição da função `getErrorMessage`.
  *Código a ser inserido:*
  ```typescript
  export function getErrorMessage(error: any, fallbackMessage: string): string {
      if (!error) return fallbackMessage
      const message = error.message || String(error)
      
      try {
          const jsonStart = message.indexOf("{")
          if (jsonStart !== -1) {
              const jsonPart = message.substring(jsonStart)
              const parsed = JSON.parse(jsonPart)
              if (parsed.error) return parsed.error
              if (parsed.message) return parsed.message
          }
      } catch (e) {
          // Ignora se não for JSON válido
      }
  
      return fallbackMessage
  }
  ```

---

### Task 2: Tratamento de Erros na UI de Pedidos

**Files:**
- Modify: `ProximoTurno/app/pedidos/page.tsx`

- [ ] **Step 1: Importar `getErrorMessage`**
  No topo do arquivo `app/pedidos/page.tsx`, adicionar o import de `getErrorMessage` vindo de `@/lib/api-service`.
  *Código de modificação:*
  Procurar a importação de `apiService` e adicionar `getErrorMessage`:
  ```typescript
  import { apiService, getErrorMessage, type ItemPedidoRenovar, type Categoria, type Pedido, type PedidoFilters, type StatusPedido } from "@/lib/api-service"
  ```

- [ ] **Step 2: Atualizar `handleRenovarTodos`**
  No bloco catch de `handleRenovarTodos`, usar `getErrorMessage`.
  *Código de modificação:*
  ```typescript
          } catch (error) {
              toast.error(getErrorMessage(error, "Erro ao renovar pedido."))
          }
  ```

- [ ] **Step 3: Atualizar `handleConfirmarRenovacao`**
  No bloco catch de `handleConfirmarRenovacao`, usar `getErrorMessage`.
  *Código de modificação:*
  ```typescript
          } catch (error) {
              toast.error(getErrorMessage(error, "Erro ao processar renovação parcial."))
          }
  ```

- [ ] **Step 4: Executar build e typecheck**
  Garantir que as alterações não contêm erros de sintaxe ou tipos.
  Run: `npx tsc --noEmit`
  Cwd: `ProximoTurno`
  Expected: Executado com sucesso, zero erros.
