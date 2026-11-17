#!/usr/bin/env node
/**
 * Chequeo de regresión de la recuperación inicial.
 *
 * Reproduce el camino que rompía la portada: la primera carga de la edición
 * falla, el lector toca "Reintentar" y el segundo intento sale bien. La app
 * tiene que dejar el spinner y mostrar la matriz.
 *
 *   node tools/check-retry-recovery.cjs [http://localhost:5173]
 *
 * Necesita la app y la API corriendo. Sale 0 si el reintento recupera la
 * pantalla, 1 si se queda girando (el bug original).
 */
const { chromium } = require('../node_modules/@playwright/test');

const BASE = process.argv[2] ?? 'http://localhost:5173';

async function main() {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });

  // React StrictMode monta dos veces y aborta el primer request, así que
  // contar llamadas es frágil. Un flag es explícito: falla hasta que el
  // test decide que el servicio volvió.
  let serviceDown = true;
  let editionCalls = 0;
  await page.route('**/api/stories*', async (route) => {
    editionCalls += 1;
    if (serviceDown) {
      await route.fulfill({ status: 500, body: 'boom' });
      return;
    }
    await route.continue();
  });

  await page.goto(BASE, { waitUntil: 'domcontentloaded' });

  const retry = page.getByRole('button', { name: 'Reintentar' });
  await retry.waitFor({ state: 'visible', timeout: 15000 });
  console.log('estado de error visible, tocando Reintentar');

  serviceDown = false;
  await retry.click();
  await page.waitForTimeout(4000);

  const state = await page.evaluate(() => ({
    spinner: Boolean(document.querySelector('.state .spinner')),
    loadingText: (document.body.textContent ?? '').includes(
      'Reuniendo las noticias del día',
    ),
    matrixRows: document.querySelectorAll('.matrix__row').length,
    errorVisible: Boolean(document.querySelector('.state__title')),
  }));

  console.log(
    `llamadas a /api/stories: ${editionCalls} · filas de matriz: ${state.matrixRows} · ` +
      `spinner: ${state.spinner} · texto de carga: ${state.loadingText}`,
  );

  if (state.matrixRows === 0 || state.spinner || state.loadingText) {
    console.error('FALLA: el reintento no saca a la app del estado de carga');
    await browser.close();
    process.exit(1);
  }

  console.log('OK: el reintento recupera la edición');

  // Segundo escenario: la actualización falla pero la edición que ya está en
  // pantalla se conserva y el aviso aparece.
  await page.route('**/api/stories?refresh=true*', (route) =>
    route.fulfill({ status: 500, body: 'boom' }),
  );
  await page.getByRole('button', { name: 'Actualizar las noticias' }).click();
  await page.waitForTimeout(1500);

  const afterRefresh = await page.evaluate(() => ({
    notice: document.querySelector('.notice')?.textContent?.trim() ?? null,
    matrixRows: document.querySelectorAll('.matrix__row').length,
  }));

  await browser.close();

  console.log(
    `aviso: ${afterRefresh.notice ? 'sí' : 'no'} · filas conservadas: ${afterRefresh.matrixRows}`,
  );

  if (!afterRefresh.notice || afterRefresh.matrixRows === 0) {
    console.error('FALLA: una actualización fallida no conserva la edición o no avisa');
    process.exit(1);
  }

  console.log('OK: la actualización fallida conserva la edición y avisa');
  process.exit(0);
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
