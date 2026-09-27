const { chromium } = require('playwright');
const fs = require('node:fs');
const path = require('node:path');
const base = process.env.B5MSHOT_TEST_URL || 'http://127.0.0.1:5078';
const out = path.resolve('release/site-qa'); fs.mkdirSync(out,{recursive:true});
(async()=>{
 const browser = await chromium.launch({channel:'msedge',headless:true});
 try {
  for(const [width,height] of [[1440,1000],[768,1000],[390,1000],[375,812],[844,390]]){
   const page=await browser.newPage({viewport:{width,height},deviceScaleFactor:1});
   const errors=[];page.on('pageerror',e=>errors.push(e.message));page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
   await page.goto(base,{waitUntil:'networkidle'});
   await page.evaluate(async()=>{await document.fonts.ready;document.querySelectorAll('.reveal').forEach(el=>el.classList.add('visible'));await Promise.all([...document.images].map(img=>{img.loading='eager';return img.decode();}));});
   await page.waitForTimeout(800);
   if(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth))throw Error('Horizontal overflow '+width);
   const missing=await page.locator('img').evaluateAll(imgs=>imgs.filter(i=>i.complete&&!i.naturalWidth).map(i=>i.src));if(missing.length)throw Error('Missing images '+missing);
   await page.screenshot({path:path.join(out,`landing-${width}.png`),fullPage:true});
   await page.locator('#playDemo').click();await page.waitForTimeout(350);
   if(await page.locator('#playDemo').getAttribute('aria-pressed')!=='true')throw Error('Demo did not start');
   await page.locator('#playDemo').click();
   if(await page.locator('#playDemo').getAttribute('aria-pressed')!=='false')throw Error('Demo did not pause');
   await page.goto(base+'/i/abcdef123456',{waitUntil:'networkidle'});
   if(await page.evaluate(()=>document.documentElement.scrollHeight>innerHeight||document.documentElement.scrollWidth>innerWidth))throw Error('Viewer scroll '+width);
   if(await page.evaluate(()=>{const s=document.querySelector('#shot').getBoundingClientRect(),a=document.querySelector('.image-stage').getBoundingClientRect();return s.top<a.top||s.bottom>a.bottom||s.left<a.left||s.right>a.right;}))throw Error('Screenshot clipped '+width);
   await page.locator('#saveAs').click();await page.waitForTimeout(300);
   if(!await page.locator('#formatMenu').isVisible())throw Error('Format menu not visible');
   await page.screenshot({path:path.join(out,`viewer-${width}.png`)});
   await page.keyboard.press('Escape');if(await page.locator('#formatMenu').isVisible())throw Error('Escape failed');
   if(errors.length)throw Error(errors.join('\n'));
   await page.close();console.log(`PASS landing + demo + viewer at ${width}px`);
  }
  const reduced=await browser.newPage({reducedMotion:'reduce'});await reduced.goto(base,{waitUntil:'networkidle'});
  if((await reduced.locator('#editorDemo').getAttribute('src')).includes('editor-demo'))throw Error('Motion autoplay');
  console.log('PASS reduced motion: static poster, explicit play');await reduced.close();
  const context=await browser.newContext();
  const response=await context.request.post(base+'/api/screenshots');if(response.status()!==405)throw Error('Read-only UI accepts mutation');
  await context.close();
  console.log('PASS read-only presentation service rejects uploads');
 } finally {await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
