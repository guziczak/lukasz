// Load pdf.js library in the worker
importScripts('https://cdnjs.cloudflare.com/ajax/libs/pdf.js/2.12.313/pdf.min.js');

// Set the path to the worker script
pdfjsLib.GlobalWorkerOptions.workerSrc = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/2.12.313/pdf.worker.min.js';

self.onmessage = function (event) {
    const { pdfId, pdfUrl } = event.data;

    // Convert PDF to image
    pdfToImage(pdfUrl)
        .then(imageUrl => {
            self.postMessage({ pdfId, imageUrl });
        })
        .catch(error => {
            console.error('PDF conversion error:', error);
            self.postMessage({ pdfId, imageUrl: null }); // Send null in case of error
        });
};

async function pdfToImage(pdfUrl) {
    try {
        const pdf = await fetch(pdfUrl);
        if (!pdf.ok) throw new Error('Failed to load PDF');

        const pdfData = await pdf.arrayBuffer();

        // Ensure the workerSrc is set before getting the document
        if (!pdfjsLib.GlobalWorkerOptions.workerSrc) {
            throw new Error('No "GlobalWorkerOptions.workerSrc" specified');
        }

        const pdfDoc = await pdfjsLib.getDocument({ data: pdfData }).promise;
        const page = await pdfDoc.getPage(1);

        const viewport = page.getViewport({ scale: 2 });
        const canvas = new OffscreenCanvas(viewport.width, viewport.height);
        const context = canvas.getContext('2d');

        await page.render({ canvasContext: context, viewport }).promise;

        // Convert OffscreenCanvas to an image
        const blob = await canvas.convertToBlob({ type: 'image/png' });
        return URL.createObjectURL(blob);
    } catch (error) {
        console.error('Error in pdfToImage:', error);
        throw error;
    }
}