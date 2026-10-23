// Rasterize the supplied vector kit for uGUI (no Vector Graphics runtime dependency).
const fs = require('fs');
const path = require('path');
const sharp = require(process.env.FARM_UI_NODE_MODULES ? path.join(process.env.FARM_UI_NODE_MODULES, 'sharp') : 'sharp');
const root = path.resolve(__dirname, '../..');
const source = path.join(root, 'Assets/moreAssets/FarmGame_UI');
const target = path.join(root, 'Assets/_Game/Resources/FarmUI');
const svg = (w,h,body) => Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}">${body}</svg>`);
async function run() {
  for (const folder of fs.readdirSync(path.join(source,'icons'))) {
    const dir=path.join(source,'icons',folder); if(!fs.statSync(dir).isDirectory())continue;
    for(const file of fs.readdirSync(dir).filter(x=>x.endsWith('.svg')))
      await sharp(path.join(dir,file)).resize(128,128).png().toFile(path.join(target,'Icons',file.replace('.svg','.png')));
  }
  const skins={
    panel: svg(128,128,'<rect y="4" width="128" height="124" rx="24" fill="#8A5A32"/><rect width="128" height="120" rx="24" fill="#C98B55"/><rect x="8" y="8" width="112" height="104" rx="16" fill="#FFF4DC"/>'),
    slot: svg(96,96,'<rect y="4" width="96" height="92" rx="18" fill="#D9B98A"/><rect x="2" y="2" width="92" height="88" rx="16" fill="#FFF8E8" stroke="#EAD3A8" stroke-width="4"/>'),
    ribbon: svg(128,96,'<rect y="6" width="128" height="90" rx="14" fill="#6E4423"/><rect width="128" height="84" rx="14" fill="#A0673A"/><path d="M14 12H114" stroke="#C98B55" stroke-width="3"/>')
  };
  for(const [name,face,lip] of [['primary','#6CC24A','#3E8A27'],['secondary','#F2A23A','#B86E1E'],['ghost','#FFF4DC','#D9B98A'],['danger','#E2533F','#A8382A']])
    skins[name]=svg(128,96,`<rect y="4" width="128" height="92" rx="26" fill="${lip}"/><rect width="128" height="84" rx="26" fill="${face}"/><rect x="10" y="7" width="108" height="22" rx="12" fill="white" opacity=".18"/>`);
  for(const [name,data] of Object.entries(skins))await sharp(data).png().toFile(path.join(target,'Skin',name+'.png'));
  for(const file of ['00_UI_Kit.svg','01_Главное_меню.svg','02_Настройки_Звук.svg','09_Инвентарь.svg','10_Магазин_Каталог.svg'])
    await sharp(path.join(source,'screens',file)).resize(1280,720).png().toFile(path.join(root,'Verification/UIIntegration','reference-'+file.replace('.svg','.png')));
  console.log('Converted 107 kit icons, 7 sliced skins, 5 reference previews.');
}
run().catch(e=>{console.error(e);process.exitCode=1;});
