using EasyTabs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using HtmlAgilityPack;

namespace pien_herkun_softa
{
    public partial class Form1 : Form
    {
        // JSON storage
        private readonly string jsonPath = "tuotteet.json";
        private List<Product> localProducts = new List<Product>();
        private Product editingProduct = null;

        Timer updateTimer = new Timer();

        public Form1()
        {
            InitializeComponent();
            SetupListView();

            LoadLocalProducts();

            this.Shown += async (s, e) => await UpdateListViewAsync();
        }

        // ---------------- PRODUCT MODEL ----------------
        public class Product
        {
            public string Name { get; set; }
            public int Maara { get; set; }
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
                    Name = name,
                    Maara = 0,
                    Tukkuhinta = 0,
                    Suositushinta = price
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
                var item = new ListViewItem(p.Name + " (Netistä)");
                item.SubItems.Add(p.Suositushinta.ToString("0.00") + " €");
                listView1.Items.Add(item);
            }

            // Local products (from JSON)
            foreach (var p in localProducts)
            {
                var item = new ListViewItem(p.Name);
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
            textBox2.Text = ""; // Määrä
            textBox3.Text = ""; // Tukkuhinta
            textBox4.Text = ""; // Suositushinta

            tabControl1.SelectedTab = tabPage2;
        }

        // button3 = Muokkaa Tuote
        private void button3_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0) return;

            var selectedItem = listView1.SelectedItems[0];
            string rawName = selectedItem.Text;

            if (rawName.EndsWith(" (Netistä)"))
            {
                MessageBox.Show("Netistä tulevia tuotteita ei voi muokata.");
                return;
            }

            string name = rawName.Replace(" (OMA)", "");
            editingProduct = localProducts.Find(x => x.Name == name);
            if (editingProduct == null) return;

            textBox1.Text = editingProduct.Name;
            textBox2.Text = editingProduct.Maara.ToString();
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
            var tuote = localProducts.Find(x => x.Name == name);
            if (tuote == null) return;

            localProducts.Remove(tuote);
            SaveLocalProducts();
            await UpdateListViewAsync();
        }

        // button6 = Tallenna (SAVE)
        private async void button6_Click(object sender, EventArgs e)
        {
            string nimi = textBox1.Text.Trim();
            int.TryParse(textBox2.Text.Trim(), out int maara);
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
                    Name = nimi,
                    Maara = maara,
                    Tukkuhinta = tukku,
                    Suositushinta = suositus
                };
                localProducts.Add(uusi);
            }
            else
            {
                // Edit existing
                editingProduct.Name = nimi;
                editingProduct.Maara = maara;
                editingProduct.Tukkuhinta = tukku;
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
        private void button5_Click(object sender, EventArgs e) { }

        private void Nettisivu_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.kauppa.piianherkut.fi/shop/");
        }
    }
}
