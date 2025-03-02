// Ładowanie biblioteki pdf.js i pdf.worker.js w workerze
importScripts(
  'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/2.12.313/pdf.min.js',
  'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/2.12.313/pdf.worker.min.js'
);

// Ustawienie ścieżki do pliku workerowego
pdfjsLib.GlobalWorkerOptions.workerSrc = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/2.12.313/pdf.worker.min.js';

self.onmessage = function (event) {
  const { pdfId, pdfUrl } = event.data;

  // Konwersja PDF na obraz
  pdfToImage(pdfUrl)
    .then(imageUrl => {
      self.postMessage({ pdfId, imageUrl });
    })
    .catch(error => {
      console.error('Błąd konwersji PDF:', error);
      self.postMessage({ pdfId, imageUrl: null }); // Wysyłanie null w przypadku błędu
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

    // Konwersja OffscreenCanvas do obrazu
    const blob = await canvas.convertToBlob({ type: 'image/png' });
    return URL.createObjectURL(blob);
  } catch (error) {
    console.error('Error in pdfToImage:', error);
    throw error;
  }
}