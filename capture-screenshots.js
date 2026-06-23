const puppeteer = require('puppeteer');
const fs = require('fs');
const path = require('path');

(async () => {
    console.log('Launching headless browser...');
    const browser = await puppeteer.launch({
        headless: "new",
        args: ['--no-sandbox', '--disable-setuid-sandbox']
    });
    const page = await browser.newPage();

    // Define screenshots directory
    const screenshotDir = path.join(__dirname, 'screenshots');
    if (!fs.existsSync(screenshotDir)){
        fs.mkdirSync(screenshotDir);
    }

    // Viewport Definitions
    const viewports = [
        { name: 'landscape-16-9', width: 1920, height: 1080 },
        { name: 'portrait-9-16', width: 1080, height: 1920 },
        { name: 'landscape-4-3', width: 1600, height: 1200 },
        { name: 'portrait-3-4', width: 1200, height: 1600 },
        { name: 'landscape-21-9', width: 2560, height: 1080 },
        { name: 'portrait-9-21', width: 1080, height: 2520 },
        { name: 'landscape-low-res', width: 800, height: 600 },
        { name: 'portrait-low-res', width: 480, height: 800 }
    ];

    for (const vp of viewports) {
        console.log(`Setting viewport: ${vp.name} (${vp.width}x${vp.height})...`);
        await page.setViewport({ width: vp.width, height: vp.height });
        
        console.log('Navigating to Wetter clock...');
        await page.goto('http://localhost:8080', { waitUntil: 'networkidle2' });
        
        // Wait 2.5 seconds to ensure animations, canvas, and charts are fully drawn
        await new Promise(resolve => setTimeout(resolve, 2500));

        const outputPath = path.join(screenshotDir, `${vp.name}.png`);
        console.log(`Capturing screenshot: ${outputPath}`);
        await page.screenshot({ path: outputPath, fullPage: false });
    }

    await browser.close();
    console.log('Screenshot capture complete!');
})();