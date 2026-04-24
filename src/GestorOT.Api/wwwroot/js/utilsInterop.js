window.utilsInterop = {
    triggerFileDownload: function (fileName, url) {
        const anchorElement = document.createElement('a');
        anchorElement.href = url;
        anchorElement.download = fileName ?? '';
        anchorElement.click();
        anchorElement.remove();
    },
    downloadFile: function (fileName, contentType, bytes) {
        const file = new Blob([new Uint8Array(bytes)], { type: contentType });
        const url = URL.createObjectURL(file);
        this.triggerFileDownload(fileName, url);
        URL.revokeObjectURL(url);
    },
    copyToClipboard: function (text) {
        if (!navigator.clipboard) {
            // Fallback for older browsers
            var textArea = document.createElement("textarea");
            textArea.value = text;
            document.body.appendChild(textArea);
            textArea.select();
            try {
                document.execCommand('copy');
            } catch (err) {
                console.error('Fallback: Oops, unable to copy', err);
            }
            document.body.removeChild(textArea);
            return;
        }
        navigator.clipboard.writeText(text).then(function () {
            console.log('Async: Copying to clipboard was successful!');
        }, function (err) {
            console.error('Async: Could not copy text: ', err);
        });
    }
};
