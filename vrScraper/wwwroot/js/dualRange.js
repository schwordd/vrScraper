window.dualRange = {
    init: function (id, dotNetRef, min, max, step, minVal, maxVal) {
        const el = document.getElementById(id);
        if (!el) return;
        const minInput = el.querySelector('.dual-range-min');
        const maxInput = el.querySelector('.dual-range-max');
        const fill = el.querySelector('.dual-range-fill');
        const minLabel = el.querySelector('.dual-range-label-min');
        const maxLabel = el.querySelector('.dual-range-label-max');

        // Clean up previous handlers if re-initializing
        if (minInput._inputHandler) minInput.removeEventListener('input', minInput._inputHandler);
        if (minInput._commitHandler) minInput.removeEventListener('mouseup', minInput._commitHandler);
        if (minInput._touchCommit) minInput.removeEventListener('touchend', minInput._touchCommit);
        if (maxInput._inputHandler) maxInput.removeEventListener('input', maxInput._inputHandler);
        if (maxInput._commitHandler) maxInput.removeEventListener('mouseup', maxInput._commitHandler);
        if (maxInput._touchCommit) maxInput.removeEventListener('touchend', maxInput._touchCommit);

        function updateFill() {
            const lo = parseInt(minInput.value);
            const hi = parseInt(maxInput.value);
            const range = max - min;
            fill.style.left = ((lo - min) / range * 100) + '%';
            fill.style.right = ((max - hi) / range * 100) + '%';
            minLabel.textContent = lo > 0 ? lo + 'm' : '0';
            maxLabel.textContent = hi < max ? hi + 'm' : 'Max';
        }

        minInput._inputHandler = function () {
            if (parseInt(minInput.value) >= parseInt(maxInput.value) - step) {
                minInput.value = parseInt(maxInput.value) - step;
            }
            updateFill();
        };
        minInput.addEventListener('input', minInput._inputHandler);

        maxInput._inputHandler = function () {
            if (parseInt(maxInput.value) <= parseInt(minInput.value) + step) {
                maxInput.value = parseInt(minInput.value) + step;
            }
            updateFill();
        };
        maxInput.addEventListener('input', maxInput._inputHandler);

        function commit() {
            dotNetRef.invokeMethodAsync('OnSliderCommit', parseInt(minInput.value), parseInt(maxInput.value));
        }

        minInput._commitHandler = commit;
        minInput._touchCommit = commit;
        minInput.addEventListener('mouseup', minInput._commitHandler);
        minInput.addEventListener('touchend', minInput._touchCommit);

        maxInput._commitHandler = commit;
        maxInput._touchCommit = commit;
        maxInput.addEventListener('mouseup', maxInput._commitHandler);
        maxInput.addEventListener('touchend', maxInput._touchCommit);

        // Set initial values
        minInput.value = minVal;
        maxInput.value = maxVal;
        updateFill();
    }
};
