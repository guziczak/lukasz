self.onmessage = function (event) {
    const { pdfId, pdfUrl } = event.data;

    // Konwersja PDF na obraz
    pdfToImage(pdfUrl)
        .then(imageUrl => {
            self.postMessage({ pdfId, imageUrl });
        })
        .catch(error => {
            console.error('Błąd konwersji PDF:', error);
        });
};

async function pdfToImage(pdfUrl) {
    const pdf = await fetch(pdfUrl);
    if (!pdf.ok) throw new Error('Failed to load PDF');

    const pdfData = await pdf.arrayBuffer();
    const pdfDoc = await pdfjsLib.getDocument({ data: pdfData }).promise;
    const page = await pdfDoc.getPage(1);

    const viewport = page.getViewport({ scale: 2 });
    const canvas = document.createElement('canvas');
    const context = canvas.getContext('2d');

    canvas.width = viewport.width;
    canvas.height = viewport.height;

    await page.render({ canvasContext: context, viewport }).promise;

    return canvas.toDataURL('image/png');
}