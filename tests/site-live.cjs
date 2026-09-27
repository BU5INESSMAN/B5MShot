// Read-only production checks: never uploads or modifies screenshots.
const { chromium } = require('playwright');
const fs = require('node:fs');
(async () => {
 const browser = await chromium.launch({channel:'msedge',headless:true});
 try {
  const page = await browser.newPage({viewport:{width:1440,height:1000}});
  const errors = [];
  page.on('pageerror', e => errors.push(e.message));
  page.on('console', m => {if(m.type()==='error') errors.push(m.text());});
  await page.goto('https://s.bu5inessman.ru/', {waitUntil:'networkidle'});
  await page.evaluate(async()=>{
   await document.fonts.ready;
   document.querySelectorAll('.reveal').forEach(el=>el.classList.add('visible'));
   await Promise.all([...document.images].map(img=>{img.loading='eager';return img.decode();}));
  });
  await page.waitForTimeout(800);
  fs.mkdirSync('release/site-qa',{recursive:true});
  await page.screenshot({path:'release/site-qa/live-1440.png',fullPage:true});
  await page.locator('#playDemo').click();
  await page.locator('#editorDemo').evaluate(img=>img.decode());
  if(await page.locator('#playDemo').getAttribute('aria-pressed')!=='true')throw Error('Playback failed');
  await page.locator('#playDemo').click();
  const request=page.context().request;
  if(await page.locator('.hero-actions .primary').getAttribute('href')!=='/download/B5MShot-Setup.exe')throw Error('Primary download is not installer');
  if(!(await page.locator('.release-note').textContent()).includes('0.8.1'))throw Error('Wrong landing release');
  for(const path of ['/download/B5MShot.exe','/download/B5MShot-Setup.exe','/assets/viewer.css']){
   const response=await request.head('https://s.bu5inessman.ru'+path);
   if(response.status()!==200)throw Error('Broken link '+path);
  }
  if(errors.length)throw Error(errors.join('\n'));
  console.log('PASS live 0.8.1: assets, fonts, demo play/stop, installer + portable downloads, no browser errors');
 } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
