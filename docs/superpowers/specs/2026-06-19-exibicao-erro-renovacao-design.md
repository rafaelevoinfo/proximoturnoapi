# Especificação de Design: Exibição de Erros do Backend na Renovação de Pedidos

## Histórico de Revisões
- **Data:** 19/06/2026
- **Status:** Em Revisão
- **Autor:** Antigravity (AI Coding Assistant)

---

## 1. Visão Geral
Atualmente, quando ocorre um erro na renovação parcial (ou total) de um pedido, o frontend exibe uma mensagem de erro genérica ("Erro ao processar renovação parcial"). Isso impede que o usuário saiba o motivo real da falha (por exemplo, "O jogo X já está reservado por outro cliente").

Este design especifica:
1. Uma função utilitária `getErrorMessage(error, fallback)` no frontend para extrair a mensagem de erro detalhada retornada pela API (embutida no formato JSON dentro da mensagem da exceção).
2. A atualização do tratamento de erros na tela de pedidos para exibir essa mensagem de erro detalhada através dos toasts do sistema.

---

## 2. Detalhes da Implementação

### 2.1. Função Utilitária `getErrorMessage` em `lib/api-service.ts`
Como o `apiService.request` lança exceções contendo o body da resposta de erro HTTP (ex: `{"error":"Mensagem detalhada..."}`), criaremos um extrator robusto:
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
        // Ignora
    }

    return fallbackMessage
}
```

### 2.2. Atualização dos Tratas de Erro em `app/pedidos/page.tsx`
Importar `getErrorMessage` e atualizar os blocos `catch` de `handleConfirmarRenovacao` e `handleRenovarTodos`:
```typescript
toast.error(getErrorMessage(error, "Erro ao processar renovação parcial."))
```
e
```typescript
toast.error(getErrorMessage(error, "Erro ao renovar pedido."))
```

---

## 3. Plano de Testes
1. Simular uma falha de validação de renovação no backend (ex: tentando renovar um jogo que já está alugado/reservado por outro usuário).
2. Disparar a renovação parcial no frontend.
3. Verificar se o toast exibe a mensagem de erro específica retornada pelo backend em vez de "Erro ao processar renovação parcial".
