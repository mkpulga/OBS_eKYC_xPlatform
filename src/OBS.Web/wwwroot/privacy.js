window.obsPrivacy = {
    open(dialog, reference) {
        if (!dialog || !dialog.isConnected) return;
        if (!dialog._privacyCancel) {
            dialog._privacyCancel = event => {
                event.preventDefault();
                if (dialog.open) dialog.close();
                reference.invokeMethodAsync('CancelPrivacyDialog').catch(() => {});
            };
            dialog.addEventListener('cancel', dialog._privacyCancel);
        }
        if (!dialog.open) dialog.showModal();
    }
};
