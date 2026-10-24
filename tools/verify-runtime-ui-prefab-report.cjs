const {chromium}=require('playwright');
const fs=require('node:fs');const path=require('node:path');const {pathToFileURL}=require('node:url');
const config=JSON.parse(fs.readFileSync('artifacts/runtime-ui-prefab-tests/paths.json','utf8').replace(/^\uFEFF/,''));
(async()=>{const browser=await chromium.launch({channel:'chrome',headless:true});const results=[];
try{for(const file of ['artifacts/development-report.html','docs/guide.html',config.forward+'/resource-port.html',config.reverse+'/resource-port.html']){
const page=await browser.newPage();const errors=[];page.on('pageerror',error=>errors.push(error.message));await page.goto(pathToFileURL(path.resolve(file)).href);
if(file===config.forward+'/resource-port.html'&&!await page.getByRole('heading',{name:'지원 범위와 주의 항목'}).count())throw Error('Native UI limitations are missing from the report');
for(const width of [1280,390]){await page.setViewportSize({width,height:900});const check=await page.evaluate(()=>({overflow:document.documentElement.scrollWidth>innerWidth,links:[...document.querySelectorAll('a')].map(a=>a.getAttribute('href'))}));
if(check.overflow)throw Error(file+' overflows at '+width);
for(const href of check.links){if(href.startsWith('#')){if(!await page.locator(href).count())throw Error('Missing anchor '+href);}else if(!/^[a-z]+:/i.test(href)&&!fs.existsSync(path.resolve(path.dirname(file),decodeURIComponent(href))))throw Error('Missing result '+href);}
if(file.endsWith('development-report.html'))await page.screenshot({path:'artifacts/runtime-ui-prefab-report-'+width+'.png',fullPage:true});results.push({file,width,overflow:false,linksVerified:true});}
if(errors.length)throw Error(errors.join('\n'));await page.close();}
fs.writeFileSync('artifacts/runtime-ui-prefab-report-browser-verification.json',JSON.stringify({status:'PASS',results},null,2));console.log('RUNTIME_REPORT_BROWSER_PASS');
}finally{await browser.close();}})().catch(error=>{console.error(error);process.exitCode=1;});














