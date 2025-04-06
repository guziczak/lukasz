using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net.Http;
using System.IO.Compression;
using ImageMagick;

/// <summary>
/// PDF to PNG Converter - z wbudowanym Ghostscript dla niezawodnej konwersji
/// </summary>
class PdfToPngConverter
{
    // Konfiguracja
    private const int RENDER_DPI = 200; // Jeszcze mniejsza rozdzielczość dla mniejszych plików
    
    // Ścieżki dla Ghostscript
    private static readonly string GS_DIR = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gs");
    private static readonly string GS_BIN_DIR = Path.Combine(GS_DIR, "bin");
    private static readonly string GS_LIB_DIR = Path.Combine(GS_DIR, "lib");
    private static readonly string GS_EXE_PATH = Path.Combine(GS_BIN_DIR, "gswin64c.exe");
    
    // Typowe ścieżki instalacji systemowej Ghostscript
    private static readonly string[] SYSTEM_GS_PATHS = {
        @"C:\Program Files\gs\gs*\bin\gswin64c.exe",
        @"C:\Program Files (x86)\gs\gs*\bin\gswin64c.exe",
        @"C:\Program Files\gs*\bin\gswin64c.exe",
        @"C:\Program Files (x86)\gs*\bin\gswin64c.exe",
        @"C:\gs\gs*\bin\gswin64c.exe"
    };
    
    // Główna metoda programu
    static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("PDF to PNG Converter - Wysokiej jakości konwerter z wbudowanym Ghostscript");
            Console.WriteLine("-------------------------");
            
            // Upewnij się, że mamy Ghostscript
            SetupPortableGhostscript();
            
            // Znajdź katalog certs
            string certsDirectory = FindCertsDirectory();
            
            // Utwórz folder tmp w katalogu certs
            string tmpFolder = Path.Combine(certsDirectory, "tmp");
            Directory.CreateDirectory(tmpFolder);
            
            Console.WriteLine($"Katalog roboczy: {certsDirectory}");
            Console.WriteLine($"Katalog wyjściowy: {tmpFolder}");
            Console.WriteLine($"Używana rozdzielczość: {RENDER_DPI} DPI");
            
            // Znajdź wszystkie pliki PDF w katalogu certs
            string[] pdfFiles = Directory.GetFiles(certsDirectory, "*.pdf", SearchOption.AllDirectories);
            
            if (pdfFiles.Length == 0)
            {
                Console.WriteLine($"Nie znaleziono plików PDF w: {certsDirectory}");
            }
            else
            {
                Console.WriteLine($"Znaleziono {pdfFiles.Length} plik(ów) PDF.");
                
                // Konwertuj pliki
                ConvertPdfFiles(pdfFiles, tmpFolder);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
        
        Console.WriteLine();
        Console.WriteLine("Naciśnij dowolny klawisz, aby zakończyć...");
        Console.ReadKey();
    }
    
    /// <summary>
    /// Konfiguruje przenośną wersję Ghostscript
    /// </summary>
    private static void SetupPortableGhostscript()
    {
        try
        {
            Console.WriteLine("Konfiguracja Ghostscript...");
            
            // Najpierw sprawdź, czy mamy już skonfigurowanego Ghostscript w katalogu aplikacji
            if (File.Exists(GS_EXE_PATH))
            {
                Console.WriteLine("Znaleziono wbudowany Ghostscript.");
                
                // Dodaj ścieżkę do zmiennych środowiskowych, aby Magick.NET mógł go znaleźć
                SetGhostscriptEnvironmentVariables();
                return;
            }
            
            // Następnie sprawdź, czy Ghostscript jest już zainstalowany w systemie
            string systemGsPath = FindSystemGhostscript();
            if (systemGsPath != null)
            {
                Console.WriteLine($"Znaleziono istniejącą instalację Ghostscript: {systemGsPath}");
                
                // Ustaw zmienną dla Magick.NET, aby używała systemowego Ghostscript
                Environment.SetEnvironmentVariable("MAGICK_GHOSTSCRIPT_PATH", systemGsPath);
                return;
            }
            
            Console.WriteLine("Brak zainstalowanego Ghostscript. Instalowanie...");
            
            // Utwórz katalogi dla Ghostscript
            Directory.CreateDirectory(GS_BIN_DIR);
            Directory.CreateDirectory(GS_LIB_DIR);
            
            // Rozpakuj wbudowany minimalny Ghostscript
            ExtractEmbeddedGhostscript();
            
            // Ustaw zmienne środowiskowe
            SetGhostscriptEnvironmentVariables();
            
            // Sprawdź, czy Ghostscript działa
            TestGhostscript();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ostrzeżenie: Problem z konfiguracją Ghostscript: {ex.Message}");
            Console.WriteLine("Program będzie kontynuował, ale konwersja może nie działać poprawnie.");
        }
    }
    
    /// <summary>
    /// Szuka istniejącej instalacji Ghostscript w systemie
    /// </summary>
    /// <returns>Ścieżka do gswin64c.exe lub pustą jeśli nie znaleziono</returns>
    private static string FindSystemGhostscript()
    {
        // Sprawdź, czy ghostscript istnieje już w PATH
        try
        {
            using (var process = new Process())
            {
                process.StartInfo.FileName = "gswin64c";
                process.StartInfo.Arguments = "--version";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                
                process.Start();
                string version = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(3000);
                
                if (process.ExitCode == 0)
                {
                    // Znajdź pełną ścieżkę
                    var wherePath = GetFullPathToCommand("gswin64c");
                    if (wherePath != null)
                    {
                        Console.WriteLine($"Ghostscript znaleziony w PATH: {wherePath} (wersja {version})");
                        return wherePath;
                    }
                    
                    Console.WriteLine($"Ghostscript dostępny w PATH, ale nie można ustalić pełnej ścieżki. Wersja {version}");
                    return "gswin64c";
                }
            }
        }
        catch
        {
            // Ghostscript nie jest dostępny w PATH, kontynuuj sprawdzanie innych lokalizacji
        }
        
        // Sprawdź typowe ścieżki instalacji
        foreach (var gsPattern in SYSTEM_GS_PATHS)
        {
            try {
                string dirPath = Path.GetDirectoryName(gsPattern);
                if (Directory.Exists(dirPath)) {
                    var matchingFiles = Directory.GetFiles(dirPath, Path.GetFileName(gsPattern), SearchOption.AllDirectories);
                    if (matchingFiles.Length > 0)
                    {
                        // Znaleziono Ghostscript, użyj najnowszej wersji (zakładając, że mają sortowanie zgodnie z wersją)
                        Array.Sort(matchingFiles);
                        return matchingFiles[matchingFiles.Length - 1];
                    }
                }
            }
            catch (Exception ex) {
                Console.WriteLine($"Ostrzeżenie przy wyszukiwaniu Ghostscript: {ex.Message}");
            }
        }
        
        // Nie znaleziono
        return null;
    }
    
    /// <summary>
    /// Zwraca pełną ścieżkę do komendy użycie where/which
    /// </summary>
    private static string GetFullPathToCommand(string command)
    {
        try
        {
            using (var process = new Process())
            {
                // Windows użyje komendy "where"
                bool isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
                process.StartInfo.FileName = isWindows ? "where" : "which";
                process.StartInfo.Arguments = command;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                
                process.Start();
                string output = process.StandardOutput.ReadLine(); // Weź pierwszą linię
                process.WaitForExit(3000);
                
                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    return output.Trim();
                }
            }
        }
        catch
        {
            // Zignoruj błędy
        }
        
        return null;
    }
    
    /// <summary>
    /// Wyodrębnia wbudowany minimalny Ghostscript
    /// </summary>
    private static void ExtractEmbeddedGhostscript()
    {
        Console.WriteLine("Wyodrębnianie minimalnej wersji Ghostscript...");
        
        // W rzeczywistej implementacji, tutaj rozpakowalibyśmy zasoby osadzone w projekcie
        // Dla uproszczenia, stworzymy minimalny zestaw plików, który obsłuży podstawowe konwersje
        
        try
        {
            // Najpierw spróbuj pobrać ze źródła internetowego
            DownloadMinimalGhostscript();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Nie udało się pobrać Ghostscript: {ex.Message}");
            
            // Jako plan awaryjny, utwórz skrypt pomocniczy wykorzystujący ImageMagick bezpośrednio
            CreateAlternativeConverter();
        }
    }
    
    /// <summary>
    /// Pobiera minimalną wersję Ghostscript
    /// </summary>
    private static void DownloadMinimalGhostscript()
    {
        // Plik ZIP zawierający minimalny zestaw plików Ghostscript
        string ghostscriptUrl = "https://github.com/ArtifexSoftware/ghostpdl-downloads/releases/download/gs1000/gs1000w64.zip";
        
        Console.WriteLine($"Pobieranie Ghostscript z: {ghostscriptUrl}");
        
        string tempZipPath = Path.Combine(Path.GetTempPath(), "gs_minimal.zip");
        
        using (var client = new HttpClient())
        {
            try
            {
                // Pobierz plik
                var response = client.GetAsync(ghostscriptUrl).Result;
                if (response.IsSuccessStatusCode)
                {
                    using (var fileStream = new FileStream(tempZipPath, FileMode.Create))
                    {
                        response.Content.CopyToAsync(fileStream).Wait();
                    }
                    
                    Console.WriteLine("Pobieranie zakończone. Rozpakowywanie plików...");
                    
                    string tempExtractPath = Path.Combine(Path.GetTempPath(), "gs_extract");
                    if (Directory.Exists(tempExtractPath))
                    {
                        Directory.Delete(tempExtractPath, true);
                    }
                    Directory.CreateDirectory(tempExtractPath);
                    
                    // Rozpakuj archiwum
                    ZipFile.ExtractToDirectory(tempZipPath, tempExtractPath);
                    
                    // Skopiuj wymagane pliki do naszej lokalizacji
                    string gsSourceDir = FindGhostscriptDirectory(tempExtractPath);
                    if (gsSourceDir != null)
                    {
                        CopyDirectory(Path.Combine(gsSourceDir, "bin"), GS_BIN_DIR);
                        CopyDirectory(Path.Combine(gsSourceDir, "lib"), GS_LIB_DIR);
                        
                        Console.WriteLine("Ghostscript został pomyślnie skonfigurowany.");
                    }
                    else
                    {
                        throw new Exception("Nie znaleziono folderu Ghostscript w pobranym archiwum.");
                    }
                    
                    // Usuń pliki tymczasowe
                    try
                    {
                        File.Delete(tempZipPath);
                        Directory.Delete(tempExtractPath, true);
                    }
                    catch { }
                }
                else
                {
                    throw new Exception($"Nie udało się pobrać pliku. Kod statusu: {response.StatusCode}");
                }
            }
            catch (Exception)
            {
                // Jeśli pobranie zip zawiedzie, spróbuj pobrać instalator exe
                DownloadGhostscriptInstaller();
            }
        }
    }
    
    /// <summary>
    /// Pobiera i instaluje Ghostscript z instalatora
    /// </summary>
    private static void DownloadGhostscriptInstaller()
    {
        string installerUrl = "https://github.com/ArtifexSoftware/ghostpdl-downloads/releases/download/gs1000/gs1000w64.exe";
        
        Console.WriteLine($"Pobieranie instalatora Ghostscript z: {installerUrl}");
        
        string tempExePath = Path.Combine(Path.GetTempPath(), "gs_setup.exe");
        
        using (var client = new HttpClient())
        {
            var response = client.GetAsync(installerUrl).Result;
            if (response.IsSuccessStatusCode)
            {
                using (var fileStream = new FileStream(tempExePath, FileMode.Create))
                {
                    response.Content.CopyToAsync(fileStream).Wait();
                }
                
                Console.WriteLine("Pobieranie zakończone. Uruchamianie cichej instalacji...");
                
                // Utwórz tymczasowy folder do zainstalowania Ghostscript
                string tempInstallDir = Path.Combine(Path.GetTempPath(), "gs_install");
                if (Directory.Exists(tempInstallDir))
                {
                    Directory.Delete(tempInstallDir, true);
                }
                Directory.CreateDirectory(tempInstallDir);
                
                // Uruchom instalator w trybie cichym
                using (var process = new Process())
                {
                    process.StartInfo.FileName = tempExePath;
                    process.StartInfo.Arguments = $"/S /D={tempInstallDir}";
                    process.StartInfo.UseShellExecute = true;
                    process.StartInfo.CreateNoWindow = true;
                    
                    process.Start();
                    process.WaitForExit(60000); // Czekaj maksymalnie minutę
                }
                
                // Skopiuj pliki z instalacji do naszego katalogu
                string binDir = Path.Combine(tempInstallDir, "bin");
                string libDir = Path.Combine(tempInstallDir, "lib");
                
                if (Directory.Exists(binDir) && Directory.Exists(libDir))
                {
                    CopyDirectory(binDir, GS_BIN_DIR);
                    CopyDirectory(libDir, GS_LIB_DIR);
                    
                    Console.WriteLine("Ghostscript został pomyślnie zainstalowany.");
                }
                else
                {
                    // Wyszukaj podfoldery, w których może być Ghostscript
                    string[] dirs = Directory.GetDirectories(tempInstallDir);
                    bool found = false;
                    
                    foreach (string dir in dirs)
                    {
                        binDir = Path.Combine(dir, "bin");
                        libDir = Path.Combine(dir, "lib");
                        
                        if (Directory.Exists(binDir) && Directory.Exists(libDir))
                        {
                            CopyDirectory(binDir, GS_BIN_DIR);
                            CopyDirectory(libDir, GS_LIB_DIR);
                            found = true;
                            break;
                        }
                    }
                    
                    if (!found)
                    {
                        throw new Exception("Nie udało się znaleźć zainstalowanych plików Ghostscript.");
                    }
                }
                
                // Usuń pliki tymczasowe
                try
                {
                    File.Delete(tempExePath);
                    Directory.Delete(tempInstallDir, true);
                }
                catch { }
            }
            else
            {
                throw new Exception($"Nie udało się pobrać instalatora. Kod statusu: {response.StatusCode}");
            }
        }
    }
    
    /// <summary>
    /// Tworzy alternatywną implementację konwersji jako ostatnia deska ratunku
    /// </summary>
    private static void CreateAlternativeConverter()
    {
        Console.WriteLine("Tworzenie alternatywnego konwertera...");
        
        // Utwórz skrypt batch, który będzie używany jako zastępstwo dla Ghostscript
        string batchContent = @"@echo off
echo Alternatywny konwerter PDF (Plan awaryjny)
echo.
echo Proszę zainstalować Ghostscript ze strony:
echo https://ghostscript.com/releases/gsdnld.html
echo.
echo Alternatywna konwersja...
exit /b 0";
        
        // Zapisz skrypt w katalogu bin
        File.WriteAllText(Path.Combine(GS_BIN_DIR, "gswin64c.bat"), batchContent);
        
        // Stwórz kopię jako .exe
        File.WriteAllText(GS_EXE_PATH, batchContent);
        
        Console.WriteLine("Utworzono alternatywny konwerter. Konwersja może nie działać poprawnie.");
    }
    
    /// <summary>
    /// Wyszukuje katalog Ghostscript w rozspakowanym archiwum
    /// </summary>
    private static string FindGhostscriptDirectory(string rootDir)
    {
        // Szukaj katalogów gs* lub Ghostscript*
        foreach (string dir in Directory.GetDirectories(rootDir))
        {
            string dirName = Path.GetFileName(dir);
            if (dirName.StartsWith("gs") || dirName.StartsWith("Ghostscript"))
            {
                return dir;
            }
            
            // Sprawdź podfoldery
            string[] subdirs = Directory.GetDirectories(dir);
            foreach (string subdir in subdirs)
            {
                string subdirName = Path.GetFileName(subdir);
                if (subdirName.StartsWith("gs") || subdirName.StartsWith("Ghostscript"))
                {
                    return subdir;
                }
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Ustawia zmienne środowiskowe dla Ghostscript
    /// </summary>
    private static void SetGhostscriptEnvironmentVariables()
    {
        try
        {
            // Dodaj ścieżkę do zmiennych środowiskowych
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            if (!path.Contains(GS_BIN_DIR))
            {
                Environment.SetEnvironmentVariable("PATH", $"{GS_BIN_DIR};{path}");
            }
            
            // Ustaw zmienną GS_LIB
            Environment.SetEnvironmentVariable("GS_LIB", GS_LIB_DIR);
            
            // Ustaw zmienną dla Magick.NET
            Environment.SetEnvironmentVariable("MAGICK_GHOSTSCRIPT_PATH", GS_EXE_PATH);
            
            Console.WriteLine("Ustawiono zmienne środowiskowe dla Ghostscript.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ostrzeżenie: Nie udało się ustawić zmiennych środowiskowych: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Testuje czy Ghostscript działa
    /// </summary>
    private static void TestGhostscript()
    {
        try
        {
            using (var process = new Process())
            {
                process.StartInfo.FileName = GS_EXE_PATH;
                process.StartInfo.Arguments = "--version";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                
                process.Start();
                string version = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(3000);
                
                if (process.ExitCode == 0)
                {
                    Console.WriteLine($"Ghostscript działa poprawnie: {version}");
                }
                else
                {
                    Console.WriteLine("Ghostscript został zainstalowany, ale występują problemy z jego działaniem.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test Ghostscript nie powiódł się: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Kopiuje zawartość katalogu rekurencyjnie
    /// </summary>
    private static void CopyDirectory(string sourceDir, string destDir)
    {
        // Utwórz katalog docelowy, jeśli nie istnieje
        if (!Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }
        
        // Kopiuj pliki
        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }
        
        // Kopiuj podkatalogi
        foreach (string dir in Directory.GetDirectories(sourceDir))
        {
            string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
        }
    }
    
    /// <summary>
    /// Znajduje katalog certs
    /// </summary>
    private static string FindCertsDirectory()
    {
        // Pobierz bieżący katalog
        string currentDirectory = Directory.GetCurrentDirectory();
        
        // Jeśli jesteśmy w PdfToPngConverter, wyjdź o dwa poziomy wyżej
        if (currentDirectory.Contains("PdfToPngConverter"))
        {
            for (int i = 0; i < 2; i++)
            {
                DirectoryInfo parent = Directory.GetParent(currentDirectory);
                if (parent != null)
                {
                    currentDirectory = parent.FullName;
                }
                else
                {
                    break;
                }
            }
        }
        
        // Sprawdź, czy ten katalog lub jego rodzic to "certs"
        if (Path.GetFileName(currentDirectory) == "certs")
        {
            return currentDirectory;
        }
        
        DirectoryInfo parentDir = Directory.GetParent(currentDirectory);
        if (parentDir != null && parentDir.Name == "certs")
        {
            return parentDir.FullName;
        }
        
        // Poszukaj katalogu certs w aktualnej ścieżce lub wyżej
        string directory = currentDirectory;
        while (!directory.EndsWith("certs") && Directory.GetParent(directory) != null)
        {
            directory = Directory.GetParent(directory).FullName;
            if (directory.EndsWith("certs"))
            {
                return directory;
            }
        }
        
        // Jeśli nie możemy znaleźć katalogu certs, użyj aktualnego katalogu
        return currentDirectory;
    }
    
    /// <summary>
    /// Konwertuje pliki PDF do PNG
    /// </summary>
    private static void ConvertPdfFiles(string[] pdfFiles, string outputFolder)
    {
        int successCount = 0;
        int errorCount = 0;
        var errorFiles = new List<string>();
        
        foreach (string pdfPath in pdfFiles)
        {
            try
            {
                Console.WriteLine($"Konwersja: {Path.GetFileName(pdfPath)}");
                
                string fileName = Path.GetFileNameWithoutExtension(pdfPath);
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                
                // Ustawienia konwersji z białym tłem
                var readSettings = new MagickReadSettings();
                readSettings.Density = new Density(RENDER_DPI);
                
                // Ustaw białe tło dla PDF - używamy ciągu tekstowego do obsługi różnych wersji ImageMagick
                readSettings.SetDefine("pdf:use-cropbox", "true");
                
                using (var images = new MagickImageCollection())
                {
                    // Wczytaj PDF
                    images.Read(pdfPath, readSettings);
                    int pageCount = images.Count;
                    
                    Console.WriteLine($"  Liczba stron: {pageCount}");
                    
                    // Konwertuj każdą stronę
                    for (int i = 0; i < pageCount; i++)
                    {
                        using (var pageImage = images[i])
                        {
                            string outputFileName = pageCount > 1 
                                ? $"{fileName}_page_{i + 1}.png" 
                                : $"{fileName}.png";
                                
                            string outputPath = Path.Combine(outputFolder, outputFileName);
                            
                            // Zapisz jako PNG bez przezroczystości
                            using (var whiteBackground = new MagickImage(MagickColors.White, pageImage.Width, pageImage.Height))
                            {
                                // Nałóż obraz na białe tło
                                whiteBackground.Composite(pageImage, CompositeOperator.Over);
                                
                                // Zapisz jako PNG
                                whiteBackground.Write(outputPath);
                            }
                            
                            // Informacje o pliku
                            long fileSize = new FileInfo(outputPath).Length;
                            string fileSizeStr = fileSize < 1024 * 1024 
                                ? $"{fileSize / 1024.0:F1} KB" 
                                : $"{fileSize / (1024.0 * 1024.0):F2} MB";
                            
                            Console.WriteLine($"  Strona {i + 1}/{pageCount}: {outputFileName} " +
                                $"({pageImage.Width}x{pageImage.Height} pikseli, {fileSizeStr})");
                        }
                    }
                }
                
                stopwatch.Stop();
                Console.WriteLine($"  Czas przetwarzania: {stopwatch.ElapsedMilliseconds / 1000.0:F1}s");
                
                successCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd przetwarzania {Path.GetFileName(pdfPath)}: {ex.Message}");
                errorCount++;
                errorFiles.Add(Path.GetFileName(pdfPath));
            }
        }
        
        // Wyświetl podsumowanie
        Console.WriteLine();
        Console.WriteLine("Konwersja PDF do PNG zakończona.");
        Console.WriteLine($"Pomyślnie przekonwertowano: {successCount} plik(ów)");
        Console.WriteLine($"Niepowodzenia konwersji: {errorCount} plik(ów)");
        
        if (errorCount > 0)
        {
            Console.WriteLine("Pliki z błędami:");
            foreach (var file in errorFiles)
            {
                Console.WriteLine($"  - {file}");
            }
        }
    }
}