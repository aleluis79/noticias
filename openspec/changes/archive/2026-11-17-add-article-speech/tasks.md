# Tasks

## 1. Tooling de tests

- [x] 1.1 Agregar `vitest`, `jsdom` y `@testing-library/react` a `web/package.json`, el script `test` y la configuración de Vitest en `web/vite.config.ts`; verificar con `cd web && pnpm test` que la suite arranca y con `pnpm build` que el build sigue pasando.
- [x] 1.2 Crear `web/src/test/setup.ts` con limpieza de `localStorage` y stubs de `matchMedia`, `scrollTo`, `scrollIntoView` y `requestAnimationFrame`; verificar con un test smoke que renderiza `App` sin errores de entorno.

## 2. Modelo puro de la cola (TDD)

- [x] 2.1 Escribir en rojo los tests de `buildSpeechQueue`: orden título → atribución → párrafos, división por oración de párrafos largos y descarte de fragmentos vacíos; verificar que fallan antes de existir `speechModel.ts`.
- [x] 2.2 Implementar lo mínimo en `web/src/speech/speechModel.ts` para poner en verde los tests de la cola; verificar que solo esos tests pasan y que los demás siguen en rojo si aún no existen.
- [x] 2.3 Escribir en rojo los tests de selección de voz (prioridad `es-AR` > `es-419` > otro `es`, respeto de la voz guardada y fallback si no existe) y de parseo de velocidad (`1`, `1.25`, `1.5`, inválidos → `1`); verificar el rojo.
- [x] 2.4 Implementar `pickSpanishVoice`, `parseSpeechRate` y las claves de preferencia en `speechModel.ts`; verificar que todos los tests del modelo pasan.

## 3. Hook de reproducción (TDD)

- [x] 3.1 Crear un `FakeSpeechSynthesis` de test y escribir en rojo los tests del hook: estado inicial `idle`, `play()` encola y habla en orden, `pause()`/`resume()`/`stop()` cambian el estado, el fin de la cola vuelve a `idle`, un error `canceled` no se reporta como fallo y un navegador sin API reporta `supported: false`; verificar el rojo.
- [x] 3.2 Implementar `web/src/speech/useArticleSpeech.ts` hasta poner en verde los tests anteriores; verificar con `pnpm test`.
- [x] 3.3 Escribir en rojo los tests de ciclo de vida y preferencias: cambiar `articleKey` cancela, cerrar (`enabled: false`) cancela, desmontar cancela, la velocidad y la voz se leen y guardan en `localStorage`, y una voz guardada inexistente cae al fallback; verificar el rojo.
- [x] 3.4 Completar el hook para poner en verde esos tests; verificar que toda la suite del hook pasa.

## 4. Controles de escucha (TDD)

- [x] 4.1 Escribir en rojo los tests de `ListeningControls`: etiqueta "Escuchar la nota", botón principal alterna Escuchar/Pausar/Reanudar, Detener deshabilitado en `idle`, grupo de velocidad con `aria-pressed`, selector de voz solo con más de una voz y estado anunciado con `aria-live`; verificar el rojo.
- [x] 4.2 Implementar `web/src/speech/ListeningControls.tsx` hasta poner en verde esos tests; verificar con `pnpm test`.
- [x] 4.3 Escribir en rojo los tests de accesibilidad básica del componente (nombres accesibles de los botones, foco y `aria-pressed` coherentes) y corregir lo que falte; verificar el verde.

## 5. Integración en el lector (TDD)

- [x] 5.1 Escribir en rojo los tests de integración de `App` con `fetch` mockeado: el control aparece solo con la nota lista, `play` habla título y primeros párrafos, cerrar la nota cancela, cambiar de medio cancela, sin soporte de voz el control no aparece y reproducir no dispara requests; verificar el rojo.
- [x] 5.2 Cablear `useArticleSpeech` y `ListeningControls` en `web/src/App.tsx` hasta poner en verde los tests de integración; verificar con `pnpm test`.
- [x] 5.3 Agregar los estilos `.listening-controls` en `web/src/App.css` con tokens existentes, targets de 2.75rem y layout responsive; verificar con `pnpm build` y revisión visual en `pnpm dev`.

## 6. Documentación

- [x] 6.1 Documentar la función de escucha y `pnpm test` en `README.md`, y la capacidad con preferencias locales en `PRODUCT.md`; verificar que los comandos y claves documentados coinciden con el código.

## 7. Verificación final

- [x] 7.1 Ejecutar `cd web && pnpm test`, `pnpm lint` y `pnpm build`; verificar que los tres terminan sin errores.
- [x] 7.2 Probar manualmente en el navegador: abrir una nota, escuchar, pausar, reanudar, detener, cambiar velocidad y voz, recargar y comprobar persistencia; registrar el resultado.

## Workflow follow-up

- Archivar el cambio con `openspec archive add-article-speech` cuando la revisión esté aprobada.
- Verificar la spec principal generada en `openspec/specs/article-speech/spec.md`.
