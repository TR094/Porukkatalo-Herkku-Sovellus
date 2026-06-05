using HtmlAgilityPack;
using iText.IO.Font.Constants;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace pien_herkun_softa
{
    public partial class Form1 : Form
    {
        // JSON storage
        private readonly string jsonPath = "data.json";
        private List<Product> localProducts = new List<Product>();
        private Product editingProduct = null;
        private List<PrintItem> previewItems = new List<PrintItem>();


        Timer updateTimer = new Timer();


        public Form1()
        {
            InitializeComponent();
            SetupListView();

            LoadLocalProducts();

            this.Shown += async (s, e) => await UpdateListViewAsync();

        }

        private void LoadPreview(List<PrintItem> items)
        {
            previewItems = items;

            dgvPreview.DataSource = null;
            dgvPreview.DataSource = previewItems;

            txtReceiver.Text = "";
            txtDate.Text = DateTime.Now.ToString("dd.MM.yyyy");
        }


        // ---------------- PRODUCT MODEL ----------------
        public class Product
        {
            public string Nimi { get; set; }
            public int Määrä { get; set; }
            public decimal Tukkuhinta { get; set; }
            public decimal Suositushinta { get; set; }
        }

        public class PrintItem
        {
            public string Nimi { get; set; }
            public int Määrä { get; set; }
            public decimal Tukkuhinta { get; set; }
            public decimal Suositushinta { get; set; }
        }



        // ---------------- JSON LOAD / SAVE ----------------
        private void LoadLocalProducts()
        {
            if (File.Exists(jsonPath))
            {
                string json = File.ReadAllText(jsonPath);
                localProducts = JsonSerializer.Deserialize<List<Product>>(json) ?? new List<Product>();
            }
        }

        private void SaveLocalProducts()
        {
            string json = JsonSerializer.Serialize(localProducts, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(jsonPath, json);
        }

        // ---------------- LISTVIEW SETUP ----------------
        private void SetupListView()
        {
            listView1.View = View.Details;
            listView1.FullRowSelect = true;
            listView1.GridLines = true;
            listView1.CheckBoxes = true;
            listView1.MultiSelect = true;

            listView1.Columns.Clear();
            listView1.Columns.Add("Tuote", 400);
            //listView1.Columns.Add("Hinta (€)", 120);
        }

        // ---------------- SCRAPER ----------------
        public async Task<List<Product>> FetchProductsAsync()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

            byte[] bytes = await client.GetByteArrayAsync("https://www.kauppa.piianherkut.fi/shop/");
            string html = Encoding.UTF8.GetString(bytes);

            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            var products = doc.DocumentNode.SelectNodes("//li[contains(@class,'product')]");
            var list = new List<Product>();

            if (products == null)
                return list;

            foreach (var p in products)
            {
                string name = p.SelectSingleNode(".//h2")?.InnerText?.Trim() ?? "N/A";
                string priceRaw = p.SelectSingleNode(".//*[contains(@class,'price')]")?.InnerText?.Trim() ?? "0";

                name = System.Net.WebUtility.HtmlDecode(name);
                priceRaw = System.Net.WebUtility.HtmlDecode(priceRaw);

                priceRaw = priceRaw
                    .Replace("€", "")
                    .Replace("\u00A0", "")
                    .Replace(",", ".")
                    .Trim();

                string numericPart = System.Text.RegularExpressions.Regex.Match(priceRaw, @"\d+(\.\d+)?").Value;

                decimal.TryParse(
                    numericPart,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out decimal price
                );

                list.Add(new Product
                {
                    Nimi = name,
                    Määrä = 0,
                    Suositushinta = price,
                    Tukkuhinta = Math.Round(price / 1.135m, 2)
                });

            }

            return list;
        }

        // ---------------- UPDATE LISTVIEW ----------------
        private async Task UpdateListViewAsync()
        {
            listView1.Items.Clear();

            var scraped = await FetchProductsAsync();

            // Scraped products
            foreach (var p in scraped)
            {
                var item = new ListViewItem(p.Nimi + " (Netistä)");
                item.SubItems.Add(p.Suositushinta.ToString("0.00") + " €");
                listView1.Items.Add(item);
            }

            // Local products (from JSON)
            foreach (var p in localProducts)
            {
                var item = new ListViewItem(p.Nimi);
                item.SubItems.Add(p.Suositushinta.ToString("0.00") + " €");
                listView1.Items.Add(item);
            }
        }

        // ---------------- FORM EVENTS ----------------
        private async void Form1_Load(object sender, EventArgs e)
        {
            await FetchProductsAsync(); // not used, but harmless
        }

        // button1 = Päivitä Tiedot
        private async void button1_Click_1(object sender, EventArgs e)
        {
            await UpdateListViewAsync();
        }

        // button4 = Lisää Uusi Tuote
        private void button4_Click(object sender, EventArgs e)
        {
            editingProduct = null;

            textBox1.Text = ""; // Nimi
            textBox3.Text = ""; // Tukkuhinta
            textBox4.Text = ""; // Suositushinta

            tabControl1.SelectedTab = tabPage2;
        }

        // button3 = Muokkaa Tuote
        private void button3_Click(object sender, EventArgs e)
        {
            // Estä usean tuotteen muokkaus
            if (listView1.SelectedItems.Count == 0)
            {
                MessageBox.Show("Valitse ensin tuote.");
                return;
            }

            if (listView1.SelectedItems.Count > 1)
            {
                MessageBox.Show("Voit muokata vain yhtä tuotetta kerrallaan.");
                return;
            }

            var selectedItem = listView1.SelectedItems[0];
            string rawName = selectedItem.Text;

            if (rawName.EndsWith(" (Netistä)"))
            {
                MessageBox.Show("Netistä tulevia tuotteita ei voi muokata.");
                return;
            }

            string name = rawName.Replace(" (OMA)", "");
            editingProduct = localProducts.Find(x => x.Nimi == name);
            if (editingProduct == null) return;

            textBox1.Text = editingProduct.Nimi;
            textBox3.Text = editingProduct.Tukkuhinta.ToString("0.00");
            textBox4.Text = editingProduct.Suositushinta.ToString("0.00");

            tabControl1.SelectedTab = tabPage2;
        }


        // button2 = Poista Tuote
        private async void button2_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0) return;

            var selectedItem = listView1.SelectedItems[0];
            string rawName = selectedItem.Text;

            if (rawName.EndsWith(" (Netistä)"))
            {
                MessageBox.Show("Netistä tulevia tuotteita ei voi poistaa.");
                return;
            }

            string name = rawName.Replace(" (Netistä)", "");
            var tuote = localProducts.Find(x => x.Nimi == name);
            if (tuote == null) return;

            localProducts.Remove(tuote);
            SaveLocalProducts();
            await UpdateListViewAsync();
        }

        // button6 = Tallenna (SAVE)
        private async void button6_Click(object sender, EventArgs e)
        {
            string nimi = textBox1.Text.Trim();
            decimal.TryParse(textBox3.Text.Trim().Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal tukku);
            decimal.TryParse(textBox4.Text.Trim().Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal suositus);

            if (string.IsNullOrWhiteSpace(nimi))
            {
                MessageBox.Show("Nimi ei voi olla tyhjä.");
                return;
            }

            if (editingProduct == null)
            {
                // New product
                var uusi = new Product
                {
                    Nimi = nimi,
                    Suositushinta = suositus,
                    Tukkuhinta = Math.Round(suositus / 1.135m, 2)
                };
                localProducts.Add(uusi);
            }
            else
            {
                // Edit existing
                editingProduct.Nimi = nimi;
                editingProduct.Suositushinta = suositus;
                editingProduct.Tukkuhinta = Math.Round(suositus / 1.135m, 2);
                editingProduct.Suositushinta = suositus;
            }

            SaveLocalProducts();
            MessageBox.Show("Tuote tallennettu.");

            editingProduct = null;
            tabControl1.SelectedTab = tabPage1;
            await UpdateListViewAsync();
        }



        // ---------------- MISC EVENTS ----------------
        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            foreach (ListViewItem item in listView1.SelectedItems)
                item.Checked = true;
        }

        private void textBox1_TextChanged(object sender, EventArgs e) { }
        private void textBox1_TextChanged_1(object sender, EventArgs e) { }
        private void label1_Click(object sender, EventArgs e) { }
        private void button5_Click(object sender, EventArgs e)
        {
            var selected = new List<PrintItem>();

            foreach (ListViewItem item in listView1.Items)
            {
                if (!item.Checked) continue;

                string name = item.Text.Replace(" (Netistä)", "");
                Product p = localProducts.Find(x => x.Nimi == name);

                if (p == null)
                {
                    decimal suositus = decimal.Parse(item.SubItems[1].Text.Replace("€", "").Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);

                    selected.Add(new PrintItem
                    {
                        Nimi = name,
                        Määrä = 1,
                        Suositushinta = suositus,
                        Tukkuhinta = Math.Round(suositus / 1.135m, 2)
                    });
                }

                else
                {
                    selected.Add(new PrintItem
                    {
                        Nimi = p.Nimi,
                        Määrä = p.Määrä > 0 ? p.Määrä : 1,
                        Tukkuhinta = Math.Round(p.Suositushinta / 1.135m, 2),
                        Suositushinta = p.Suositushinta
                    });
                }
            }

            LoadPreview(selected);
            tabControl1.SelectedTab = tabPage3;
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            tabControl1.SelectedTab = tabPage1;
        }

        private void btnGeneratePdf_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "PDF Files (*.pdf)|*.pdf";
                sfd.FileName = "lähetyslista.pdf";

                if (sfd.ShowDialog() != DialogResult.OK)
                    return;

                CreatePdf(sfd.FileName);
            }
        }

        private void CreatePdf(string path)
        {
            var writer = new PdfWriter(path);
            var pdf = new PdfDocument(writer);
            var doc = new Document(pdf);

            PdfFont bold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            PdfFont normal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

            // LOGO
            Image logo = new Image(ImageDataFactory.Create("logo.png"))
                .ScaleToFit(60, 60)
                .SetMarginBottom(-30);
            doc.Add(logo);

            // TITLE
            var title = new Paragraph("LÄHETYSLISTA")
                .SetFont(bold)
                .SetFontSize(12)
                .SetTextAlignment(TextAlignment.RIGHT)
                .SetMarginTop(0)
                .SetMarginBottom(20);
            doc.Add(title);

            // HEADER BLOCK (Receiver left, Date right)
            Table header = new Table(2).UseAllAvailableWidth();

            header.AddCell(new Cell()
                .Add(new Paragraph("Vastaanottaja:\n" + txtReceiver.Text))
                .SetBorder(Border.NO_BORDER)
                .SetFont(normal)
                .SetFontSize(12));

            header.AddCell(new Cell()
                .Add(new Paragraph("Lähetyspäivä: " + txtDate.Text))
                .SetTextAlignment(TextAlignment.RIGHT)
                .SetBorder(Border.NO_BORDER)
                .SetFont(normal)
                .SetFontSize(12));


            doc.Add(header);

            // FOOTER NOTE
            doc.Add(new Paragraph("\nTukkuhinta alv 0%, suositushinta sisältää arvonlisäveron 13,5%.")
                .SetFontSize(10)
                .SetFont(bold));

            doc.Add(new Paragraph("\n"));

            // PRODUCT TABLE (same layout as real company)
            Table table = new Table(new float[] { 4, 1, 2, 2 })
                .UseAllAvailableWidth();

            // HEADER ROW
            table.AddHeaderCell(new Cell().Add(new Paragraph("Tuote").SetFont(bold).SetFontSize(12).SetFontColor(ColorConstants.BLACK)).SetBackgroundColor(ColorConstants.LIGHT_GRAY));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Määrä").SetFont(bold).SetFontSize(12).SetFontColor(ColorConstants.BLACK)).SetBackgroundColor(ColorConstants.LIGHT_GRAY));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Tukkuhinta").SetFont(bold).SetFontSize(12).SetFontColor(ColorConstants.BLACK)).SetBackgroundColor(ColorConstants.LIGHT_GRAY));
            table.AddHeaderCell(new Cell().Add(new Paragraph("Suositushinta").SetFont(bold).SetFontSize(12).SetFontColor(ColorConstants.BLACK)).SetBackgroundColor(ColorConstants.LIGHT_GRAY));

            // ROWS
            foreach (var item in previewItems)
            {
                table.AddCell(new Cell().Add(new Paragraph(item.Nimi)));

                table.AddCell(new Cell().Add(new Paragraph(item.Määrä.ToString())));

                decimal tukku = Math.Round(item.Suositushinta / 1.135m, 2);
                string tukkuStr = tukku.ToString("0.00").Replace(".", ",");

                table.AddCell(new Cell().Add(new Paragraph(tukkuStr)));

                string suositusStr = item.Suositushinta.ToString("0.00").Replace(".", ",");
                table.AddCell(new Cell().Add(new Paragraph(suositusStr)));
            }

            doc.Add(table);

            doc.Close();

            MessageBox.Show("PDF luotu onnistuneesti");
        }


        private void Nettisivu_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.kauppa.piianherkut.fi/shop/");
        }

        private void txtDate_TextChanged(object sender, EventArgs e)
        {

        }

        private void button7_Click(object sender, EventArgs e)
        {
            tabControl1.SelectedTab = tabPage1;
        }
    }
}
