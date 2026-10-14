window.obsLifecycle = {
    callback: null,
    attach(reference) {
        this.detach();
        this.callback = () => reference.invokeMethodAsync('VisibilityChanged', !document.hidden).catch(() => {});
        document.addEventListener('visibilitychange', this.callback);
        this.callback();
    },
    detach() {
        if (this.callback) document.removeEventListener('visibilitychange', this.callback);
        this.callback = null;
    }
};
