const fs = require('fs');
const path = require('path');
const refDir = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/Tools/原始传奇-资源提取/原始传奇-资源参考';
const cfgDir = 'D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/Mir2Res_Crystal/Crystal/Config';

const refItem = JSON.parse(fs.readFileSync(path.join(refDir, '1config10.25341.1.json'), 'utf8')).item;
// 参考库所有物品用到的列号（并集）
const cols = {};
for (const k of Object.keys(refItem)) {
    for (const c of Object.keys(refItem[k])) cols[c] = (cols[c] || 0) + 1;
}
const colArr = Object.keys(cols).map(Number).sort((a, b) => a - b);
console.log('参考库 item 字段 = 数字列号，共 ' + colArr.length + ' 列:');
console.log(colArr.join(', '));

const ours = JSON.parse(fs.readFileSync(path.join(cfgDir, 'ItemInfo.json'), 'utf8'))['$values'];
const ourFields = Object.keys(ours[0]);
console.log('\n我们的 ItemInfo 字段 = 具名字段，共 ' + ourFields.length + ' 个:');
console.log(ourFields.join(', '));

console.log('\n结论: 两边没有共享的字段名。参考库是 Mir2 原生"列号"格式，我们是 KFramework 的"具名"格式，Schema 完全不同。');
