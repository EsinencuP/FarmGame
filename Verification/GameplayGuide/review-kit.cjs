const fs = require('fs');
const path = require('path');
const sharp = require('C:/Users/esinencuP/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root = path.resolve(__dirname, '../..');
const kit = path.join(root, 'Assets/moreAssets/FarmGame_UI');
(async () => {
  const screens = fs.readdirSync(path.join(kit, 'screens')).filter(x => x.endsWith('.svg')).sort();
  const tiles = await Promise.all(screens.map(async (name, i) => ({input: await sharp(path.join(kit, 'screens', name)).resize(480,270).png().toBuffer(), left:(i%3)*480, top:Math.floor(i/3)*270})));
  await sharp({create:{width:1440,height:Math.ceil(screens.length/3)*270,channels:4,background:'#f7f0e5'}}).composite(tiles).png().toFile(path.join(__dirname,'Kit-screens.png'));
  await sharp(path.join(kit,'00_all_icons_sheet.svg')).resize({width:1600}).png().toFile(path.join(__dirname,'Kit-icons.png'));
  fs.writeFileSync(path.join(__dirname,'Kit-inventory.json'),JSON.stringify({screens,icons:fs.readdirSync(path.join(kit,'icons')).filter(x=>fs.statSync(path.join(kit,'icons',x)).isDirectory()).map(folder=>({folder,icons:fs.readdirSync(path.join(kit,'icons',folder)).filter(x=>x.endsWith('.svg'))}))},null,2));
})();
