using System;
using System.Diagnostics; // Dosya ve klasör açmak için şart
using System.IO;          // Dosya işlemleri için
using System.Linq;
using System.Windows.Forms;
using YoutubeExplode;     // YouTube kütüphanesi
using YoutubeExplode.Videos.Streams;

namespace YouTubeMuzikIndirici
{
    public partial class Form1 : Form
    {
        // YouTube istemcisini başta oluşturmuyoruz (Hızlı açılış için)
        private YoutubeClient youtube;

        public Form1()
        {
            InitializeComponent();
        }

        // Form Yüklenirken (Sadece temel ayarlar)
        private void Form1_Load(object sender, EventArgs e)
        {
            this.CenterToScreen();

            // Eğer tasarımda "chkSor" (Bana Sor kutusu) varsa işaretini kaldır
            if (Controls.Find("chkSor", true).Length > 0 && chkSor != null)
                chkSor.Checked = false;
        }

        // Form Ekrana Geldikten Sonra (Pano kontrolü burada yapılırsa program donmaz)
        private void Form1_Shown(object sender, EventArgs e)
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string gelenYazi = Clipboard.GetText();
                    // Link gerçekten YouTube linki mi?
                    if (!string.IsNullOrWhiteSpace(gelenYazi) &&
                       (gelenYazi.Contains("youtube.com") || gelenYazi.Contains("youtu.be")))
                    {
                        txtUrl.Text = gelenYazi;
                        // İmleci sona al
                        txtUrl.SelectionStart = txtUrl.Text.Length;
                    }
                }
            }
            catch { /* Pano hatası olursa görmezden gel */ }
        }

        // --- İNDİR BUTONU ---
        private async void btnIndir_Click(object sender, EventArgs e)
        {
            string url = txtUrl.Text;

            // 1. Link Kontrolü
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("Lütfen geçerli bir YouTube linki yapıştırın.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Arayüzü Kilitle
                btnIndir.Enabled = false;
                progressBar1.Value = 0;
                progressBar1.Style = ProgressBarStyle.Marquee; // Bekleme animasyonu
                lblDurum.Text = "Video bilgileri alınıyor...";

                // İstemciyi sadece ihtiyaç duyulduğunda oluştur (Performans)
                if (youtube == null) youtube = new YoutubeClient();

                // 2. Video Bilgilerini Çek
                var video = await youtube.Videos.GetAsync(url);
                var streamManifest = await youtube.Videos.Streams.GetManifestAsync(video.Id);

                // Sadece Ses -> En Yüksek Kalite
                var streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

                if (streamInfo == null)
                {
                    MessageBox.Show("Bu videodan ses dosyası alınamadı!", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    ResetUI();
                    return;
                }

                // 3. Dosya Yolunu Hazırla
                // Yasaklı karakterleri temizle (Örn: Tarkan / Yolla -> Tarkan _ Yolla)
                string temizBaslik = string.Join("_", video.Title.Split(Path.GetInvalidFileNameChars()));
                string tamDosyaYolu = "";

                // Kullanıcı "Bana Sor" dediyse sor, demediyse Masaüstüne indir
                bool sorsunMu = (chkSor != null && chkSor.Checked);

                if (sorsunMu)
                {
                    using (SaveFileDialog sfd = new SaveFileDialog())
                    {
                        sfd.FileName = temizBaslik + ".mp3";
                        sfd.Filter = "MP3 Dosyası|*.mp3";
                        sfd.Title = "Kaydedilecek Yeri Seçin";
                        progressBar1.Style = ProgressBarStyle.Blocks;

                        if (sfd.ShowDialog() == DialogResult.OK)
                            tamDosyaYolu = sfd.FileName;
                        else
                        {
                            lblDurum.Text = "İşlem iptal edildi.";
                            ResetUI();
                            return;
                        }
                    }
                }
                else
                {
                    // Otomatik Masaüstü
                    string masaustu = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    tamDosyaYolu = Path.Combine(masaustu, temizBaslik + ".mp3");
                }

                // 4. İndirme İşlemi
                progressBar1.Style = ProgressBarStyle.Blocks;
                lblDurum.Text = "İndiriliyor...";

                var progress = new Progress<double>(p =>
                {
                    progressBar1.Value = (int)(p * 100);
                    lblDurum.Text = $"İndiriliyor... %{(int)(p * 100)}";
                });

                await youtube.Videos.Streams.DownloadAsync(streamInfo, tamDosyaYolu, progress);

                // 5. Bitiş
                lblDurum.Text = "Tamamlandı!";

                DialogResult cevap = MessageBox.Show(
                    $"{video.Title}\nBaşarıyla indirildi!\n\nHemen çalmak ister misiniz?",
                    "İndirme Başarılı",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (cevap == DialogResult.Yes)
                {
                    // Varsayılan müzik çalar ile aç
                    Process.Start(new ProcessStartInfo(tamDosyaYolu) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Bir hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblDurum.Text = "Hata oluştu.";
            }
            finally
            {
                ResetUI();
            }
        }



        // Arayüzü sıfırlayan yardımcı metod
        private void ResetUI()
        {
            btnIndir.Enabled = true;
            progressBar1.Style = ProgressBarStyle.Blocks;
            progressBar1.Value = 0;
        }
    }
}