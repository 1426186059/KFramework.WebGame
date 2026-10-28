const fs = require('fs');
const path = require('path');
const refDir = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/Tools/原始传奇-资源提取/原始传奇-资源参考';

const c2 = JSON.parse(fs.readFileSync(path.join(refDir, '2config10.25341.1.json'), 'utf8'));
console.log('2config.box 类型:', Array.isArray(c2.box) ? 'array len ' + c2.box.length : typeof c2.box);
if (c2.box && !Array.isArray(c2.box)) {
    const bk = Object.keys(c2.box);
    console.log('box 键数:', bk.length, ' 样例键:', bk.slice(0,5));
    const sample = c2.box[bk[0]];
    console.log('box[0] 字段:', Object.keys(sample).join(','));
    console.log('box[0] 内容: ', JSON.stringify(sample).slice(0, 300));
}
// 赤月恶魔 bossDrop=50700003 是否在 box 里？
if (c2.box) {
    const bk = Object.keys(c2.box);
    console.log('box 是否含 50700003:', bk.includes('50700003') || bk.includes(50700003));
}

// 1config.item 是否含 50600001（验证 bossDrop 是否指向物品 id）
const it = JSON.parse(fs.readFileSync(path.join(refDir, '1config10.25341.1.json'), 'utf8')).item;
console.log('item 是否含 50600001:', !!(it['50600001']), it['50600001'] ? ('名=' + (it['50600001']['2']||it['50600001']['3'])) : '');
