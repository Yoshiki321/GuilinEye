(function () {
  var $ = function (id) { return document.getElementById(id); };
  var wish = $('wish');
  var current = null;
  var timer = null;

  wish.addEventListener('input', function () { $('count').textContent = wish.value.length; });

  $('presets').addEventListener('click', function (e) {
    var b = e.target.closest('.chip');
    if (!b) return;
    wish.value = b.dataset.text;
    wish.dispatchEvent(new Event('input'));
  });

  $('send').addEventListener('click', async function () {
    var text = wish.value.trim();
    if (!text || text.length > 12) { alert('愿字限 1—12 字'); return; }
    $('send').disabled = true;
    try {
      var r = await GE.api('/c03/blessings', {
        method: 'POST',
        key: 'bl_' + Date.now(),
        body: { text: text }
      });
      current = r;
      show(r);
      poll();
      loadState();
    } catch (err) {
      alert(err.message);
    } finally {
      $('send').disabled = false;
      wish.value = '';
      wish.dispatchEvent(new Event('input'));
    }
  });

  function show(r) {
    $('result').classList.add('show');
    $('code').textContent = r.status === 'queued' ? '已入河' : '排队中';
    $('statusLine').textContent = r.status === 'queued' ? '河灯已放出，正在漂向东侧' : '当前 12 盏已满，你的愿在队列里';
    $('note').textContent = r.status === 'queued'
      ? '灯号 ' + r.id + '；如现场屏未出现，请稍候 3—5 秒刷新。'
      : '前方还有 ' + r.queuePosition + ' 盏；可在本页等待，也可先去别处。';
  }

  function poll() {
    if (timer) clearInterval(timer);
    timer = setInterval(async function () {
      if (!current) return;
      try {
        var r = await GE.api('/c03/blessings/' + current.id);
        if (r.status === 'displaying' || r.status === 'done') {
          $('statusLine').textContent = r.status === 'displaying' ? '河灯已进入画面' : '本轮已放完';
          $('code').textContent = r.status === 'displaying' ? '在河中' : '已放完';
          if (r.status === 'done') { clearInterval(timer); timer = null; }
        }
      } catch (e) { /* 保持轮询 */ }
    }, 3000);
  }

  async function loadState() {
    try {
      var s = await GE.api('/c03/state');
      $('active').textContent = s.active;
      $('total').textContent = s.total;
    } catch (e) { /* 离线不影响表单 */ }
  }

  loadState();
})();
