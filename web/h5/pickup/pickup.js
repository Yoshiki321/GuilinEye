(function () {
  var $ = function (id) { return document.getElementById(id); };
  var pattern = 'tuanshan', color = 'shilv', size = 'M', sku = 'tuanshan';

  function bindChips(boxId, key, fn) {
    $(boxId).addEventListener('click', function (e) {
      var b = e.target.closest('.chip');
      if (!b) return;
      Array.prototype.forEach.call($(boxId).children, function (c) { c.classList.remove('on'); });
      b.classList.add('on');
      fn(b.dataset[key]);
    });
  }

  bindChips('patterns', 'pattern', function (v) { pattern = v; });
  bindChips('colors', 'color', function (v) { color = v; });
  bindChips('sizes', 'size', function (v) { size = v; });

  async function load() {
    try {
      var d = await GE.api('/c05/catalog');
      $('catalog').innerHTML = '';
      d.items.forEach(function (it, i) {
        var el = document.createElement('div');
        el.className = 'sku' + (i === 0 ? ' on' : '');
        el.dataset.sku = it.skuId;
        el.innerHTML = '<h4>' + it.name + '</h4><p>' + (it.price == null ? '价格待馆方确认' : '¥' + it.price) +
          '</p><span class="tag">' + (it.status === 'pending' ? '演示态' : it.status) + '</span>';
        el.addEventListener('click', function () {
          Array.prototype.forEach.call($('catalog').children, function (c) { c.classList.remove('on'); });
          el.classList.add('on');
          sku = it.skuId;
        });
        $('catalog').appendChild(el);
      });
    } catch (err) {
      $('catalog').innerHTML = '<p class="note">接口未连接：' + err.message + '</p>';
    }
  }

  $('order').addEventListener('click', async function () {
    $('order').disabled = true;
    try {
      var r = await GE.api('/c05/orders', {
        method: 'POST',
        key: 'od_' + Date.now(),
        body: { pattern: pattern, color: color, size: size, skuId: sku }
      });
      $('result').classList.add('show');
      $('code').textContent = r.pickupCode;
      $('note').textContent = '订单 ' + r.orderId + '；印章停留 ' + r.sealSeconds +
        ' 秒。当前为演示态，未发生支付；正式流程以馆方收银小票为准。';
    } catch (err) {
      alert(err.message);
    } finally {
      $('order').disabled = false;
    }
  });

  load();
})();
