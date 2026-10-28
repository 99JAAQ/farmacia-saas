# 0004. MediatR 14 en su edición Community

- **Estado:** Aceptada
- **Fecha:** 2026-10-22

## Contexto

Para CQRS se quería MediatR, que es el estándar de facto en .NET. A partir de la versión 13, MediatR tiene licencia comercial, con una edición Community gratuita para organizaciones con ingresos menores a USD 5 millones. La 12.5 es la última con licencia Apache, pero ya no recibe parches.

## Decisión

Usar **MediatR 14, edición Community**. La clave de licencia va en user-secrets (`MediatR:LicenseKey`), fuera del repositorio. Sin la clave, MediatR funciona igual y solo registra un aviso en el log.

## Alternativas consideradas

- **MediatR 12.5 (Apache):** es gratis sin condiciones, pero queda sin parches de seguridad ni soporte para versiones nuevas de .NET.
- **Un despachador propio** (una interfaz `IRequestHandler` con resolución desde DI): son unas 50 líneas, pero se pierden los *pipeline behaviors* y es una pieza más que mantener.
- **Llamar a los handlers directamente desde los controllers:** es posible, pero cada controller dependería de muchos handlers.

## Consecuencias

- **Se usa una librería conocida**, fácil de explicar en una entrevista y con soporte.
- **La licencia hay que renovarla** (la actual vence en octubre de 2027) y revisar las condiciones si el negocio supera el umbral de ingresos.
- **Los handlers no dependen de MediatR más allá de `IRequestHandler`.** Si hiciera falta, cambiar a un despachador propio sería mecánico.
