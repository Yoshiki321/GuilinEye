(function () {
  var age = '小学低年级';
  var items = [];
  var index = 0;
  var right = 0;
  var streak = 0;
  var drops = 0;
  var level = 1;
  var sid = GE.sessionId();
  var answered = false;

  var $ = function (id) { return document.getElementById(id); };
  var ageBox = $('ages');

  ageBox.addEventListener('click', function (e) {
    var b = e.target.closest('.chip');
    if (!b) return;
    Array.prototype.forEach.call(ageBox.children, function (c) { c.classList.remove('on'); });
    b.classList.add('on');
    age = b.dataset.age;
  });

  $('start').addEventListener('click', load);

  async function load() {
    try {
      var data = await GE.api('/b04/questions?age=' + encodeURIComponent(age));
      items = data.items.slice(0, 10);
      if (!items.length) throw new Error('该年龄组暂无题目');
      index = 0; right = 0; streak = 0; drops = 0; level = 1;
      $('stage').style.display = 'block';
      $('cert').classList.remove('show');
      render();
    } catch (err) {
      alert(err.message);
    }
  }

  function render() {
    answered = false;
    var q = items[index];
    $('progress').textContent = '第 ' + (index + 1) + ' 题 / 共 ' + items.length + ' 题';
    $('level').textContent = ['', '初阶 · L1', '进阶 · L2', '高阶 · L3', '诗境 · L4'][level];
    $('bar').style.width = (index / items.length * 100) + '%';
    $('prompt').firstChild.nodeValue = q.prompt;
    $('title').textContent = '《' + q.title + '》 · ' + q.age;
    $('opts').innerHTML = '';
    q.options.forEach(function (text, i) {
      var b = document.createElement('button');
      b.className = 'opt';
      b.textContent = String.fromCharCode(65 + i) + '　' + text;
      b.addEventListener('click', function () { answer(i, b); });
      $('opts').appendChild(b);
    });
    $('feedback').classList.remove('show');
    $('next').style.display = 'none';
  }

  async function answer(i, btn) {
    if (answered) return;
    answered = true;
    var q = items[index];
    try {
      var r = await GE.api('/b04/answers', {
        method: 'POST',
        key: 'ans_' + sid + '_' + q.id + '_' + i,
        body: { sessionId: sid, questionId: q.id, answer: i, age: age }
      });
      Array.prototype.forEach.call($('opts').children, function (b, k) {
        b.disabled = true;
        if (k === i) b.classList.add(r.correct ? 'good' : 'bad');
      });
      $('feedback').textContent = (r.correct ? '答对了。' : '再想想。') + r.explanation +
        (r.dropChar ? '　「' + r.dropChar + '」正在落到墙上。' : '');
      $('feedback').classList.add('show');
      if (r.correct) { right++; streak++; drops++; }
      else { streak = 0; }
      level = r.level;
      $('right').textContent = right;
      $('streak').textContent = streak;
      $('drops').textContent = drops;
      $('next').style.display = 'inline-flex';
      $('next').textContent = index + 1 >= items.length ? '查看结果' : '下一题';
    } catch (err) {
      answered = false;
      $('feedback').textContent = err.message;
      $('feedback').classList.add('show');
    }
  }

  $('next').addEventListener('click', function () {
    index++;
    if (index >= items.length) return finish();
    render();
  });

  function finish() {
    $('bar').style.width = '100%';
    $('progress').textContent = '本轮结束';
    $('prompt').firstChild.nodeValue = '答对 ' + right + ' 题，' + (drops ? '留下 ' + drops + ' 个残影。' : '下次再来。');
    $('title').textContent = 'B-04 墙面会保留最近 12 个落字';
    $('opts').innerHTML = '';
    $('next').style.display = 'none';
    if (right >= 3) $('cert').classList.add('show');
  }

  $('claim').addEventListener('click', async function () {
    try {
      var r = await GE.api('/b04/certificates', {
        method: 'POST',
        key: 'cert_' + sid,
        body: { sessionId: sid }
      });
      $('certOut').innerHTML = '电子证书已生成，凭证 <b>' + r.token + '</b>。免费数字版；烫金版请到厅外打印点。';
    } catch (err) {
      $('certOut').textContent = err.message;
    }
  });
})();
