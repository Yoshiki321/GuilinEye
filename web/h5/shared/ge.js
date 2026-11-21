/* 桂林之眼 H5 公共：API 封装 + 离线 mock + 小工具 */
(function (global) {
  var API = (location.protocol === 'file:' || location.port === '5500')
    ? 'http://127.0.0.1:8080/api/v1'
    : location.origin + '/api/v1';

  function qs(name) {
    return new URLSearchParams(location.search).get(name);
  }

  async function api(path, opt) {
    opt = opt || {};
    var headers = { 'Content-Type': 'application/json' };
    if (opt.key) headers['Idempotency-Key'] = opt.key;
    var res = await fetch(API + path, {
      method: opt.method || 'GET',
      headers: headers,
      body: opt.body ? JSON.stringify(opt.body) : undefined
    });
    var json = await res.json();
    if (!json.ok) {
      var err = new Error((json.error && json.error.message) || '请求失败');
      err.code = json.error && json.error.code;
      throw err;
    }
    return json.data;
  }

  function sessionId() {
    var key = 'ge_sid';
    var v = localStorage.getItem(key);
    if (!v) {
      v = 'h5_' + Math.random().toString(36).slice(2, 10);
      localStorage.setItem(key, v);
    }
    return v;
  }

  function fmt(n) { return String(n).padStart(2, '0'); }

  global.GE = { api: api, qs: qs, sessionId: sessionId, fmt: fmt, API: API };
})(window);
