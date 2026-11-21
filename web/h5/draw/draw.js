(function () {
  var $ = function (id) { return document.getElementById(id); };
  var pad = $('pad');
  var ctx = pad.getContext('2d');
  var painting = false;
  var color = '#1b2320';
  var size = 6;
  var paper = '宣纸';
  var paperColor = { '宣纸': '#fbf8ee', '绢帛': '#f1e6cf', '自由': '#ffffff' };

  function fillPaper() {
    ctx.fillStyle = paperColor[paper];
    ctx.fillRect(0, 0, pad.width, pad.height);
    ctx.strokeStyle = 'rgba(23,33,30,.08)';
    ctx.lineWidth = 1;
    for (var x = 0; x <= pad.width; x += 80) {
      ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, pad.height); ctx.stroke();
    }
    for (var y = 0; y <= pad.height; y += 80) {
      ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(pad.width, y); ctx.stroke();
    }
  }
  fillPaper();

  $('papers').addEventListener('click', function (e) {
    var b = e.target.closest('.chip');
    if (!b) return;
    Array.prototype.forEach.call($('papers').children, function (c) { c.classList.remove('on'); });
    b.classList.add('on');
    paper = b.dataset.paper;
    fillPaper();
  });

  $('colors').addEventListener('click', function (e) {
    var s = e.target.closest('.swatch');
    if (!s) return;
    Array.prototype.forEach.call($('colors').children, function (c) { c.classList.remove('on'); });
    s.classList.add('on');
    color = s.dataset.color;
  });

  $('size').addEventListener('input', function (e) { size = +e.target.value; });

  $('clear').addEventListener('click', fillPaper);

  function point(e) {
    var r = pad.getBoundingClientRect();
    return {
      x: (e.clientX - r.left) * pad.width / r.width,
      y: (e.clientY - r.top) * pad.height / r.height
    };
  }

  pad.addEventListener('pointerdown', function (e) {
    painting = true;
    pad.setPointerCapture(e.pointerId);
    var p = point(e);
    ctx.beginPath(); ctx.moveTo(p.x, p.y);
  });
  pad.addEventListener('pointermove', function (e) {
    if (!painting) return;
    var p = point(e);
    ctx.strokeStyle = color;
    ctx.lineWidth = size;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    ctx.lineTo(p.x, p.y);
    ctx.stroke();
  });
  pad.addEventListener('pointerup', function () { painting = false; });
  pad.addEventListener('pointerleave', function () { painting = false; });

  $('submit').addEventListener('click', async function () {
    var nick = $('nickname').value.trim();
    if (!nick) nick = '游客';
    $('submit').disabled = true;
    try {
      var dataUrl = pad.toDataURL('image/jpeg', 0.55);
      var r = await GE.api('/c04/works', {
        method: 'POST',
        key: 'wk_' + GE.sessionId() + '_' + Date.now(),
        body: { nickname: nick, paper: paper, image: dataUrl }
      });
      $('result').classList.add('show');
      $('slot').textContent = '第 ' + (r.slot + 1) + ' 格';
      $('note').textContent = '已入队；今日第 ' + r.dailyUsed + ' / ' + r.dailyMax +
        ' 幅。窗口开放后进入长卷；每 ' + r.collapseEvery + ' 秒收拢一次，动画 ' + r.collapseDur + ' 秒。';
      fillPaper();
    } catch (err) {
      alert(err.message);
    } finally {
      $('submit').disabled = false;
    }
  });
})();
