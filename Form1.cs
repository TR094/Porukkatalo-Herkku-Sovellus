using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using EasyTabs;

namespace pien_herkun_softa
{
	public partial class Form1 : Form
	{
		List<string> productList = new List<string>();
		Timer updateTimer = new Timer();

		public Form1()
		{
			InitializeComponent();

			updateTimer.Interval = 10000;
			updateTimer.Tick += async (s, e) => await UpdateProductsAsync();
			updateTimer.Start();

			textBox1.GotFocus += (s, e) => this.ActiveControl = null;

			// run immediately on startup
			_ = UpdateProductsAsync();
		}


		private async Task UpdateProductsAsync()
		{
			await FetchProductsAsync();
			textBox1.Clear();

			int index = 1;
			foreach (string item in productList)
			{
				textBox1.AppendText($"{index}. {item}" + Environment.NewLine);
				index++;
			}
		}

		public async Task FetchProductsAsync()
		{
			var client = new HttpClient();
			client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

			byte[] bytes = await client.GetByteArrayAsync("https://www.kauppa.piianherkut.fi/shop/");
			string html = Encoding.UTF8.GetString(bytes);


			var doc = new HtmlAgilityPack.HtmlDocument();
			doc.LoadHtml(html);

			var products = doc.DocumentNode.SelectNodes("//li[contains(@class,'product')]");
			if (products == null)
			{
				MessageBox.Show("No products found.");
				return;
			}

			productList.Clear();

			foreach (var p in products)
			{
				string name = p.SelectSingleNode(".//h2")?.InnerText?.Trim() ?? "N/A";
				string price = p.SelectSingleNode(".//*[contains(@class,'price')]")?.InnerText?.Trim() ?? "N/A";

				name = System.Net.WebUtility.HtmlDecode(name);
				price = System.Net.WebUtility.HtmlDecode(price);

				productList.Add($"{name} — {price}");
			}
		}

		private void textBox1_TextChanged_1(object sender, EventArgs e)
		{

		}

		private async void Form1_Load(object sender, EventArgs e)
		{
			await FetchProductsAsync();
		}

		private async void button1_Click_1(object sender, EventArgs e)
		{
			textBox1.Clear();

			int index = 1;
			foreach (string item in productList)
			{
				textBox1.AppendText($"{index}. {item}" + Environment.NewLine);
				index++;
			}
		}
	}




	namespace pien_herkun_softa
	{
		public class TabsMain : TitleBarTabs
		{
			public TabsMain()
			{
				TabRenderer = new ChromeTabRenderer(this);

				Tabs.Add(new TitleBarTab(this)
				{
					Content = new Form1 { Text = "Hinnasto" }
				});

				Tabs.Add(new TitleBarTab(this)
				{
					Content = new FormTuotteet { Text = "Tuotteet" }
				});

				SelectedTabIndex = 0;
			}

			public override TitleBarTab CreateTab()
			{
				return new TitleBarTab(this)
				{
					Content = new Form1 { Text = "Hinnasto" }
				};
			}
		}
	}

	public partial class FormTuotteet : Form
	{
		public FormTuotteet()
		{

		}
	}
}