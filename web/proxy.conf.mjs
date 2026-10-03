// Same-origin in development: the dev server forwards API and hub calls to the YARP gateway (address injected by Aspire).
const target = process.env['services__gateway__http__0'] ?? 'http://localhost:5300';

export default {
  '/api': { target, secure: false, changeOrigin: true },
  '/hubs': { target, secure: false, changeOrigin: true, ws: true },
};
