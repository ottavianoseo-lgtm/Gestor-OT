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
    }
};
