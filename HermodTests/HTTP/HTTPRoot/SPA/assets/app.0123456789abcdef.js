/*! Test bundle for the single-page application tests. It only has to be
 *  larger than the minimum compression size (1024 bytes) and compress well,
 *  which repetitive text does; it is never executed. */
(function () {
    'use strict';
    var messages = [
        'The quick brown fox jumps over the lazy dog. 0000000000',
        'The quick brown fox jumps over the lazy dog. 1111111111',
        'The quick brown fox jumps over the lazy dog. 2222222222',
        'The quick brown fox jumps over the lazy dog. 3333333333',
        'The quick brown fox jumps over the lazy dog. 4444444444',
        'The quick brown fox jumps over the lazy dog. 5555555555',
        'The quick brown fox jumps over the lazy dog. 6666666666',
        'The quick brown fox jumps over the lazy dog. 7777777777',
        'The quick brown fox jumps over the lazy dog. 8888888888',
        'The quick brown fox jumps over the lazy dog. 9999999999',
        'The quick brown fox jumps over the lazy dog. aaaaaaaaaa',
        'The quick brown fox jumps over the lazy dog. bbbbbbbbbb',
        'The quick brown fox jumps over the lazy dog. cccccccccc',
        'The quick brown fox jumps over the lazy dog. dddddddddd',
        'The quick brown fox jumps over the lazy dog. eeeeeeeeee',
        'The quick brown fox jumps over the lazy dog. ffffffffff'
    ];
    function render(root) {
        root.textContent = messages.join('\n');
    }
    var app = document.getElementById('app');
    if (app !== null) {
        render(app);
    }
})();
