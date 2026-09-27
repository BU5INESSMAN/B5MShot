// Rebuild raster/platform assets from the editable vector master. Requires sharp.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('sharp');
const root = path.resolve(__dirname, '..');
const svg = fs.readFileSync(path.join(root, 'src/B5MShot.App/Assets/logo.svg'));
async function main() {
  await sharp(svg).resize(256,256).png().toFile(path.join(root,'src/B5MShot.App/Assets/logo.png'));
  const sizes=[16,24,32,48,64,128,256];
  const icons=await Promise.all(sizes.map(s=>sharp(svg).resize(s,s).png().toBuffer()));
  const header=Buffer.alloc(6+16*sizes.length); header.writeUInt16LE(1,2); header.writeUInt16LE(sizes.length,4);
  let offset=header.length;
  icons.forEach((image,i)=>{const pos=6+i*16; header[pos]=header[pos+1]=sizes[i]===256?0:sizes[i]; header.writeUInt16LE(1,pos+4);header.writeUInt16LE(32,pos+6);header.writeUInt32LE(image.length,pos+8);header.writeUInt32LE(offset,pos+12);offset+=image.length;});
  fs.writeFileSync(path.join(root,'src/B5MShot.App/Assets/logo.ico'),Buffer.concat([header,...icons]));
  for(const [name,w,h] of [['StoreLogo',50,50],['Square44x44Logo',44,44],['Square150x150Logo',150,150],['Wide310x150Logo',310,150]]) {
    const mark=await sharp(svg).resize(Math.min(w,h),Math.min(w,h)).png().toBuffer();
    await sharp({create:{width:w,height:h,channels:4,background:'#00000000'}}).composite([{input:mark,gravity:'centre'}]).png().toFile(path.join(root,`packaging/Assets/${name}.png`));
  }
  const web=path.join(root,'src/B5MShot.Server/wwwroot/assets'); fs.mkdirSync(web,{recursive:true});
  fs.copyFileSync(path.join(root,'src/B5MShot.App/Assets/logo.png'),path.join(web,'logo.png'));
  fs.copyFileSync(path.join(root,'src/B5MShot.App/Assets/logo.ico'),path.join(web,'favicon.ico'));
  fs.copyFileSync(path.join(root,'src/B5MShot.App/Assets/logo.svg'),path.join(web,'logo.svg'));
  const inner=svg.toString().replace(/^<svg[^>]*>/,'').replace(/<\/svg>\s*$/,'');
  const og=`<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="630"><defs><linearGradient id="bg" x2="1" y2="1"><stop stop-color="#edf5ff"/><stop offset="1" stop-color="#c8dbf3"/></linearGradient></defs><rect width="1200" height="630" fill="url(#bg)"/><circle cx="1120" cy="60" r="330" fill="#fff" opacity=".4"/><g transform="translate(840 176)">${inner}</g><text x="76" y="151" font-family="Segoe UI" font-size="28" font-weight="600" fill="#1768d4">B5MShot / Windows</text><text x="72" y="270" font-family="Segoe UI" font-size="72" font-weight="700" fill="#172233">Поймай момент.</text><text x="72" y="354" font-family="Segoe UI" font-size="72" font-weight="700" fill="#172233">Поделись мыслью.</text><text x="76" y="439" font-family="Segoe UI" font-size="27" fill="#4f6178">Снимок. Пометки. Готовая ссылка.</text><rect x="76" y="490" width="248" height="57" rx="28" fill="#1269dd"/><text x="113" y="527" font-family="Segoe UI" font-size="21" font-weight="600" fill="#fff">s.bu5inessman.ru</text></svg>`;
  await sharp(Buffer.from(og)).png().toFile(path.join(web,'og.png'));
  fs.copyFileSync(path.join(web,'og.png'),path.join(root,'src/B5MShot.Server/Assets/og.png'));
  console.log('Brand assets generated from logo.svg');
}
main().catch(error=>{console.error(error);process.exitCode=1;});
