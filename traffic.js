 
const http = require('http');

const BASE_URL = 'http://localhost:5025';

const NORMAL_ROUTES = ['/api/products', '/api/categories', '/api/users', '/api/orders'];
const RATE_LIMITED_ROUTE = '/api/login'; 

function sendRequest(path) {
  return new Promise((resolve) => {
    const req = http.get(`${BASE_URL}${path}`, (res) => {
      let data = '';
      res.on('data', chunk => data += chunk);
      res.on('end', () => {
        const cacheHeader = res.headers['x-cache'] || 'NONE';
        console.log(`[${new Date().toLocaleTimeString()}] ${res.statusCode} | Path: ${path.padEnd(16)} | X-Cache: ${cacheHeader}`);
        resolve(res.statusCode);
      });
    });

    req.on('error', (err) => {
      console.log(`[${new Date().toLocaleTimeString()}] FAIL | Path: ${path.padEnd(16)} | Error: ${err.message}`);
      resolve(null);
    });
  });
}

setInterval(async () => {
  const randomRoute = NORMAL_ROUTES[Math.floor(Math.random() * NORMAL_ROUTES.length)];
  await sendRequest(randomRoute);
}, 800); 

setInterval(async () => {
  console.log('\n⚡ [Burst Attack] إرسال طلبات مكثفة لمسار /api/login لتفعيل الـ Rate Limit...');
  for (let i = 0; i < 7; i++) {
    sendRequest(RATE_LIMITED_ROUTE);
  }
}, 10000); 
