const {chromium}=require('playwright');
const fs=require('node:fs');const path=require('node:path');const {pathToFileURL}=require('node:url');
(async()=>{const browser=await chromium.launch({channel:'chrome',headless:true});const results=[];
try{for(const file of ['artifacts/development-report.html','docs/guide.html','artifacts/runtime-component-roundtrip-v7/PROJECT-82bd0ee94218/resource-port.html']){
const page=await browser.newPage();const errors=[];page.on('pageerror',error=>errors.push(error.message));await page.goto(pathToFileURL(path.resolve(file)).href);
for(const width of [1280,390]){await page.setViewportSize({width,height:900});const check=await page.evaluate(()=>({overflow:document.documentElement.scrollWidth>innerWidth,links:[...document.querySelectorAll('a')].map(a=>a.getAttribute('href'))}));
if(check.overflow)throw Error(file+' overflows at '+width);
for(const href of check.links){if(href.startsWith('#')){if(!await page.locator(href).count())throw Error('Missing anchor '+href);}else if(!/^[a-z]+:/i.test(href)&&!fs.existsSync(path.resolve(path.dirname(file),decodeURIComponent(href))))throw Error('Missing result '+href);}
if(file.endsWith('development-report.html'))await page.screenshot({path:'artifacts/runtime-component-report-'+width+'.png',fullPage:true});results.push({file,width,overflow:false,linksVerified:true});}
if(errors.length)throw Error(errors.join('\n'));await page.close();}
fs.writeFileSync('artifacts/runtime-component-report-browser-verification.json',JSON.stringify({status:'PASS',results},null,2));console.log('RUNTIME_REPORT_BROWSER_PASS');
}finally{await browser.close();}})().catch(error=>{console.error(error);process.exitCode=1;});



