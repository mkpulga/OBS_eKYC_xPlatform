window.obsApplication = {
    showDialog(dialog) { if (!dialog.open) dialog.showModal(); },
    scrollTo(id) {
        const section = document.getElementById(id);
        if (section) { section.scrollIntoView({ behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'instant' : 'smooth', block: 'center' }); section.focus({ preventScroll: true }); }
    },
    documentUrl(bytes, mime) { return URL.createObjectURL(new Blob([bytes], { type: mime })); },
    releaseUrl(url) { URL.revokeObjectURL(url); }
};
