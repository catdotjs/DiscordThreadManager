using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using System.Reflection;
using System.Threading;

namespace DiscordThreadManager {
    public partial class MainWindow : Window {
        public HttpClient Client = new HttpClient();
        public List<Thread> Threads = new List<Thread>();
        public DateTime DiscordEpoch = new DateTime(2015,1,1);

        private readonly string[] LockEmoji = { "🔒" , "  \u2006\u2006\u2006\u2006\u2006" };

        public MainWindow() {
            InitializeComponent();
            Client.BaseAddress = new Uri("https://discord.com/api/v10/");
            Token.Password = Environment.GetEnvironmentVariable("THREAD_MANAGER_TOKEN");
        }

        private async void Button_Click(object sender, RoutedEventArgs e) {
            //Clear and re-write
            Threads.Clear();
            ThreadList.Items.Clear();

            //Need to await Get Thread first
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", Token.Password.Trim());
            Threads.Clear();
            await GetThreads($"guilds/{GuildId.Text}/threads/active", true);
            await GetThreads($"channels/{ChannelId.Text}/threads/archived/public", false);
            WriteToListBox();
        }

        private async Task GetThreads(string apiPath, bool isActive) {
            try {
                HttpResponseMessage resp = await Client.GetAsync(apiPath);
                if (!resp.IsSuccessStatusCode) {
                    throw new Exception("Discord API Error");
                }

                string respString = await resp.Content.ReadAsStringAsync();
                JObject respJSON = JObject.Parse(respString);
                JArray threads = (JArray) respJSON["threads"];
                foreach (JObject thread in threads) {
                    if ((string) thread["parent_id"] == ChannelId.Text & (string) thread["flags"] != "2") {
                        Threads.Add(new Thread((string) thread["id"], 
                            (string) thread["name"], 
                            (string) thread["last_message_id"], 
                            isActive, 
                            (bool) thread["thread_metadata"]["locked"]
                        ));
                    }
                }
            } catch(Exception ex) {
                MessageBox.Show(ex.Message);
            }
        }

        private string ThreadToString(Thread thread) {
            DateTime TimeOfLastMessage = DiscordEpoch.AddMilliseconds((long) thread.LastMessageTime);
            TimeSpan timeSpan = DateTime.UtcNow - TimeOfLastMessage;

            string time = timeSpan.Days > 3 ? $"{Math.Round(timeSpan.TotalDays, 1)} days" : $"{Math.Round(timeSpan.TotalHours, 1)} hours";
            string emojis = (thread.isLocked ? LockEmoji[0] : LockEmoji[1]) + (thread.isActive ? "🔴" : "❌");

            return $"{emojis} {thread.Name} ({time})";
        }

        private void WriteToListBox() {
            ThreadList.Items.Clear();
            Threads = Threads.OrderByDescending(a => a.LastMessageTime).ToList();
            foreach (Thread thread in Threads) {
                ThreadList.Items.Add(ThreadToString(thread));
            }
        }

        private async void Archive_UnArchive_Click(object sender, RoutedEventArgs e) {
            int index = ThreadList.SelectedIndex;
            Archive_UnArchive.IsEnabled = false;
            try { 
                Thread currentThread = Threads[index];
                HttpRequestMessage patchThread = new HttpRequestMessage(new HttpMethod("PATCH"), $"channels/{currentThread.Id}");

                patchThread.Content = new StringContent($"{{\"archived\":{currentThread.isActive}}}".ToLower(), Encoding.UTF8, "application/json");
                patchThread.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                HttpResponseMessage resp = await Client.SendAsync(patchThread);
                if (!resp.IsSuccessStatusCode){
                    throw new Exception($"Discord API Error - {resp.StatusCode} \n{await resp.Content.ReadAsStringAsync()}");
                }
                // MessageBox.Show($"Action was successful!");
                currentThread.isActive = !currentThread.isActive;
                Threads[index] = currentThread;
            }
            catch(Exception ex) {
                MessageBox.Show(ex.Message);
            }
            WriteToListBox();
            ThreadList.SelectedIndex = index;
            Archive_UnArchive.IsEnabled = true;
        }

        private async void Lock_Unlock_Click(object sender, RoutedEventArgs e) {
            int index = ThreadList.SelectedIndex;
            Lock_Unlock.IsEnabled = false;
            try {
                Thread currentThread = Threads[index];
                HttpRequestMessage patchThread = new HttpRequestMessage(new HttpMethod("PATCH"), $"channels/{currentThread.Id}");

                patchThread.Content = new StringContent($"{{\"locked\":{!currentThread.isLocked}, \"archived\": true}}".ToLower(), Encoding.UTF8, "application/json");
                patchThread.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                HttpResponseMessage resp = await Client.SendAsync(patchThread);
                if (!resp.IsSuccessStatusCode){
                    throw new Exception($"Discord API Error - {resp.StatusCode} \n{await resp.Content.ReadAsStringAsync()}");
                }
                // MessageBox.Show($"Action was successful!");
                currentThread.isActive = false;
                currentThread.isLocked = !currentThread.isLocked;
                Threads[index] = currentThread;
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message);
            }
            WriteToListBox();
            ThreadList.SelectedIndex = index;     
            Lock_Unlock.IsEnabled = true;
        }

        private void Open_Thread_Click(object sender, RoutedEventArgs e) {
            try {
                Thread currentThread = Threads[ThreadList.SelectedIndex];
                Process.Start($"discord://-/channels/{GuildId.Text}/{ChannelId.Text}/threads/{currentThread.Id}");
            } catch (Exception ex) {
                MessageBox.Show(ex.Message);
            }
        }

        private void ThreadList_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
            Open_Thread_Click(sender, e);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e) {
            switch (e.Key) {
                case Key.Enter:
                    Open_Thread_Click(sender, e);
                    break;
                case Key.O:
                    Lock_Unlock_Click(sender, e);
                    break;
                case Key.P:
                    Archive_UnArchive_Click(sender, e);
                    break;
            }
        }
    }
}
