window.dualRange = {
    init: function (id, dotNetRef, min, max, step, minVal, maxVal) {
        const el = document.getElementById(id);
        if (!el) return;
        const minInput = el.querySelector('.dual-range-min');
        const maxInput = el.querySelector('.dual-range-max');
        const fill = el.querySelector('.dual-range-fill');
        const minLabel = el.querySelector('.dual-range-label-min');
        const maxLabel = el.querySelector('.dual-range-label-max');

        function updateFill() {
            const lo = parseInt(minInput.value);
            const hi = parseInt(maxInput.value);
            const range = max - min;
            fill.style.left = ((lo - min) / range * 100) + '%';
            fill.style.right = ((max - hi) / range * 100) + '%';
            minLabel.textContent = lo > 0 ? lo + 'm' : '0';
            maxLabel.textContent = hi < max ? hi + 'm' : 'Max';
        }

        minInput.addEventListener('input', function () {
            if (parseInt(minInput.value) >= parseInt(maxInput.value) - step) {
                minInput.value = parseInt(maxInput.value) - step;
            }
            updateFill();
        });

        maxInput.addEventListener('input', function () {
            if (parseInt(maxInput.value) <= parseInt(minInput.value) + step) {
                maxInput.value = parseInt(minInput.value) + step;
            }
            updateFill();
        });

        function commit() {
            dotNetRef.invokeMethodAsync('OnSliderCommit', parseInt(minInput.value), parseInt(maxInput.value));
        }

        minInput.addEventListener('mouseup', commit);
        minInput.addEventListener('touchend', commit);
        maxInput.addEventListener('mouseup', commit);
        maxInput.addEventListener('touchend', commit);

        // Set initial values
        minInput.value = minVal;
        maxInput.value = maxVal;
        updateFill();
    }
};
