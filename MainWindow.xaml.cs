using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Drawing;
using IronOcr;

namespace PDFEditorPro
{
    public partial class MainWindow : Window
    {
        private string путьPDF = "";
        private string режим = "";

        public MainWindow() => InitializeComponent();

        private void Лог(string сообщение) => Статус.Text = сообщение;

        private void ОткрытьPDF(object s, RoutedEventArgs e)
        {
            var диалог = new OpenFileDialog { Filter = "PDF|*.pdf" };
            if (диалог.ShowDialog() == true)
            {
                путьPDF = диалог.FileName;
                ПросмотрPDF.Navigate(new Uri(путьPDF));
                Лог($"✓ Открыт: {Path.GetFileName(путьPDF)}");
            }
        }

        private void СоздатьPDF(object s, RoutedEventArgs e)
        {
            var диалог = new SaveFileDialog { Filter = "PDF|*.pdf", DefaultExt = ".pdf" };
            if (диалог.ShowDialog() != true) return;

            var док = new PdfDocument();
            var страница = док.AddPage();
            var графика = XGraphics.FromPdfPage(страница);
            графика.DrawString("Новый PDF", new XFont("Arial", 24, XFontStyleEx.Bold), XBrushes.Black, 100, 100);
            док.Save(диалог.FileName);
            путьPDF = диалог.FileName;
            ПросмотрPDF.Navigate(new Uri(путьPDF));
            Лог("✓ Создан новый документ");
        }

        private void СохранитьPDF(object s, RoutedEventArgs e) => Лог("✓ Сохранено");

        private void РежимТекста(object s, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(путьPDF)) { MessageBox.Show("Откройте PDF"); return; }
            режим = "текст";
            ХолстТекста.Visibility = Visibility.Visible;
            ПанельИнструментов.Visibility = Visibility.Visible;
            Режим.Text = "✏️ РЕЖИМ: Добавление текста";
            ТекстОСR.Document.Blocks.Clear();
            Лог("Введите текст справа и кликните на документ");
        }

        private void РежимФото(object s, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(путьPDF)) { MessageBox.Show("Откройте PDF"); return; }
            режим = "фото";
            var диалог = new OpenFileDialog { Filter = "Изображения|*.jpg;*.png;*.bmp" };
            if (диалог.ShowDialog() != true) return;

            try
            {
                var док = PdfReader.Open(путьPDF, PdfDocumentOpenMode.Modify);
                var графика = XGraphics.FromPdfPage(док.Pages[0]);
                var изображение = XImage.FromFile(диалог.FileName);
                графика.DrawImage(изображение, 200, 300, 180, 150);
                док.Save(путьPDF);
                ПросмотрPDF.Navigate(new Uri(путьPDF));
                Лог("✓ Изображение добавлено");
                ОтменитьРежим(null, null);
            }
            catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
        }

        private void КликХолстаТекста(object s, MouseButtonEventArgs e)
        {
            if (режим != "текст") return;

            var диапазон = new TextRange(ТекстОСR.Document.ContentStart, ТекстОСR.Document.ContentEnd);
            string текст = диапазон.Text.Trim();
            if (string.IsNullOrEmpty(текст)) { MessageBox.Show("Введите текст справа"); return; }

            Point позиция = e.GetPosition(ХолстТекста);

            try
            {
                var док = PdfReader.Open(путьPDF, PdfDocumentOpenMode.Modify);
                var графика = XGraphics.FromPdfPage(док.Pages[0]);
                
                string размер = (РазмерШрифта.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "14";
                double.TryParse(размер, out double размерШ);
                if (размерШ == 0) размерШ = 14;

                XBrush кисть = XBrushes.Black;
                string цвет = (ЦветТекста.SelectedItem as ComboBoxItem)?.Content.ToString();
                if (цвет == "Красный") кисть = XBrushes.Red;
                else if (цвет == "Синий") кисть = XBrushes.Blue;
                else if (цвет == "Зелёный") кисть = XBrushes.Green;

                var шрифт = new XFont("Arial", размерШ, XFontStyleEx.Regular);
                графика.DrawString(текст, шрифт, кисть, new XPoint(позиция.X, позиция.Y));
                док.Save(путьPDF);
                ПросмотрPDF.Navigate(new Uri(путьPDF));
                ОтменитьРежим(null, null);
                Лог("✓ Текст добавлен в документ");
            }
            catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
        }

        private void ОтменитьРежим(object s, RoutedEventArgs e)
        {
            режим = "";
            ХолстТекста.Visibility = Visibility.Collapsed;
            ПанельИнструментов.Visibility = Visibility.Collapsed;
            Режим.Text = "Режим просмотра";
            Лог("✓ Режим отменён");
        }

        private void ПрименитьРежим(object s, RoutedEventArgs e) => ОтменитьРежим(null, null);

        private void РаспознатьPDF(object s, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(путьPDF)) { MessageBox.Show("Откройте PDF"); return; }

            var язык = MessageBox.Show("Русский язык?", "Выбор языка", MessageBoxButton.YesNo) == MessageBoxResult.Yes ? 
                       OcrLanguage.Russian : OcrLanguage.English;

            try
            {
                Лог("⏳ Распознавание текста из PDF...");
                var OCR = new IronTesseract { Language = язык };
                var результат = OCR.Read(путьPDF);

                ТекстОСR.Document.Blocks.Clear();
                if (результат != null && !string.IsNullOrEmpty(результат.Text))
                {
                    var пар = new Paragraph(new Run(результат.Text) { Background = Brushes.Yellow, Foreground = Brushes.Black });
                    ТекстОСR.Document.Blocks.Add(пар);
                    Лог($"✓ Распознано: {результат.Text.Length} символов");
                }
            }
            catch (Exception ex) { MessageBox.Show("Ошибка OCR: " + ex.Message); }
        }

        private void ВставитьВДокумент(object s, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(путьPDF)) return;
            var диапазон = new TextRange(ТекстОСR.Document.ContentStart, ТекстОСR.Document.ContentEnd);
            string текст = диапазон.Text;
            if (string.IsNullOrEmpty(текст)) return;

            try
            {
                var док = PdfReader.Open(путьPDF, PdfDocumentOpenMode.Modify);
                var графика = XGraphics.FromPdfPage(док.Pages[0]);
                графика.DrawString(текст, new XFont("Arial", 10), XBrushes.Black, 40, 40);
                док.Save(путьPDF);
                ПросмотрPDF.Navigate(new Uri(путьPDF));
                Лог("✓ Распознанный текст вставлен в документ");
            }
            catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
        }

        private void КопироватьТекст(object s, RoutedEventArgs e)
        {
            var диапазон = new TextRange(ТекстОСR.Document.ContentStart, ТекстОСR.Document.ContentEnd);
            if (!string.IsNullOrEmpty(диапазон.Text)) Clipboard.SetText(диапазон.Text);
            Лог("✓ Скопировано");
        }

        private void ОчиститьТекст(object s, RoutedEventArgs e)
        {
            ТекстОСR.Document.Blocks.Clear();
            Лог("✓ Очищено");
        }

        private void УдалитьСтраницу(object s, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(путьPDF)) return;
            var док = PdfReader.Open(путьPDF, PdfDocumentOpenMode.Modify);
            if (док.PageCount <= 1) { MessageBox.Show("Только одна страница"); return; }
            string ввод = Microsoft.VisualBasic.Interaction.InputBox("Номер страницы:", "");
            if (int.TryParse(ввод, out int п) && п > 0 && п <= док.PageCount)
            {
                док.Pages.RemoveAt(п - 1);
                док.Save(путьPDF);
                ПросмотрPDF.Navigate(new Uri(путьPDF));
                Лог($"✓ Страница {п} удалена");
            }
        }

        private void ОбъединитьPDF(object s, RoutedEventArgs e)
        {
            var д1 = new OpenFileDialog { Filter = "PDF|*.pdf" };
            if (д1.ShowDialog() != true) return;
            var д2 = new OpenFileDialog { Filter = "PDF|*.pdf" };
            if (д2.ShowDialog() != true) return;
            var д3 = new SaveFileDialog { Filter = "PDF|*.pdf", DefaultExt = ".pdf" };
            if (д3.ShowDialog() != true) return;

            var выход = new PdfDocument();
            var док1 = PdfReader.Open(д1.FileName, PdfDocumentOpenMode.Import);
            for (int i = 0; i < док1.PageCount; i++) выход.AddPage(док1.Pages[i]);
            var док2 = PdfReader.Open(д2.FileName, PdfDocumentOpenMode.Import);
            for (int i = 0; i < док2.PageCount; i++) выход.AddPage(док2.Pages[i]);
            выход.Save(д3.FileName);
            Лог("✓ PDF объединены");
        }

        private void РазделитьPDF(object s, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(путьPDF)) return;
            var док = PdfReader.Open(путьPDF, PdfDocumentOpenMode.Import);
            string нач = Microsoft.VisualBasic.Interaction.InputBox("Начало:", "");
            string конец = Microsoft.VisualBasic.Interaction.InputBox("Конец:", "");
            if (int.TryParse(нач, out int н) && int.TryParse(конец, out int к) && н > 0 && к <= док.PageCount && н <= к)
            {
                var д = new SaveFileDialog { Filter = "PDF|*.pdf", DefaultExt = ".pdf" };
                if (д.ShowDialog() != true) return;
                var выход = new PdfDocument();
                for (int i = н - 1; i < к; i++) выход.AddPage(док.Pages[i]);
                выход.Save(д.FileName);
                Лог("✓ PDF разделён");
            }
        }
    }
}
