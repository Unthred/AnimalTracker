// Positions popovers in the viewport stacking layer (above in-flow siblings in long forms).
// constrainListHeight: pass false for tall panels (e.g. date calendar); default true for combobox lists.
window.animalTrackerAnchorFixedBelow = function (trigger, el, gapPx, constrainListHeight) {
    if (!trigger || !el) return;
    var g = typeof gapPx === 'number' ? gapPx : 4;
    var constrain = constrainListHeight !== false;
    var r = trigger.getBoundingClientRect();
    el.style.position = 'fixed';
    el.style.left = Math.max(8, r.left) + 'px';
    el.style.top = (r.bottom + g) + 'px';
    el.style.width = r.width + 'px';
    el.style.zIndex = '200000';
    if (constrain) {
        el.style.maxHeight = 'min(60vh, 15rem)';
        el.style.overflowY = 'auto';
    } else {
        el.style.removeProperty('max-height');
        el.style.removeProperty('overflow-y');
    }
};

window.animalTrackerClearFixedAnchor = function (el) {
    if (!el) return;
    el.style.removeProperty('position');
    el.style.removeProperty('left');
    el.style.removeProperty('top');
    el.style.removeProperty('width');
    el.style.removeProperty('z-index');
    el.style.removeProperty('max-height');
    el.style.removeProperty('overflow-y');
};
