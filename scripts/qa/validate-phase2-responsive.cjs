const fs = require("fs");
const path = require("path");
const { chromium } = require("playwright-core");

const chrome = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
const base = "http://localhost:8080";
const workspace = "44babeb0-7f43-4619-83f7-6fce5ddd43c5";
const output = path.resolve("artifacts/phase2-qa");
const sizes = [[360,800],[768,900],[1024,900],[1440,1000]];
const routes = ["/workspaces",`/workspaces/${workspace}/dashboard`,`/workspaces/${workspace}/devices`,
  `/workspaces/${workspace}/explorer`,"/status"];

(async()=>{
  fs.mkdirSync(output,{recursive:true}); const browser=await chromium.launch({headless:true,executablePath:chrome}); const evidence=[];
  for(const [width,height] of sizes){
    const context=await browser.newContext({viewport:{width,height}}); const page=await context.newPage();
    page.on("pageerror",e=>console.error("pageerror",e.message));
    page.on("console",m=>{if(m.type()==="error")console.error("console",m.text())});
    await page.goto(`${base}/login`); await page.getByLabel("Email").fill("phase2-qa@fluxo.local");
    await page.getByLabel("Senha").fill("Abcdef!23456"); await page.getByRole("button",{name:"Entrar"}).click();
    await page.waitForURL("**/workspaces");
    for(const route of routes){
      await page.evaluate(route=>{history.pushState({},"",route);window.dispatchEvent(new PopStateEvent("popstate"));},route);
      await page.waitForTimeout(250); await page.waitForLoadState("networkidle");
      if(route.endsWith("/explorer")){
        await page.getByLabel("Maquina QA").check(); await page.getByText("temperature_c").waitFor();
        for(const key of ["temperature_c","current_a","door_open","machine_state"]) await page.getByLabel(new RegExp(key)).check();
        await page.getByRole("button",{name:"Executar consulta"}).click(); await page.getByText(/Numeric ·/).first().waitFor();
        await page.getByRole("heading",{name:"Boolean · sem unidade"}).waitFor(); await page.getByRole("heading",{name:"Text · sem unidade"}).waitFor();
      }
      const overflow=await page.evaluate(()=>({innerWidth:window.innerWidth,scrollWidth:document.documentElement.scrollWidth,
        title:document.querySelector("h1")?.textContent??"",errors:[...document.querySelectorAll(".error-message")].map(x=>x.textContent)}));
      const name=route.replace(/^\//,"").replaceAll("/","-")||"root"; const screenshot=`${width}-${name}.png`;
      await page.screenshot({path:path.join(output,screenshot),fullPage:true}); evidence.push({width,height,route,screenshot,...overflow});
      if(overflow.scrollWidth>overflow.innerWidth) throw new Error(`horizontal overflow at ${width}px ${route}: ${overflow.scrollWidth}`);
      if(overflow.errors.length) throw new Error(`UI error at ${route}: ${overflow.errors.join("; ")}`);
    }
    await context.close();
  }
  await browser.close(); fs.writeFileSync(path.join(output,"report.json"),JSON.stringify(evidence,null,2));
  console.log(`validated ${evidence.length} page/viewports; evidence=${output}`);
})().catch(async e=>{console.error(e);process.exit(1)});
