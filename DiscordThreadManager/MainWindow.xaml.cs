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
using System.Runtime.InteropServices;

namespace DiscordThreadManager {

    public partial class MainWindow : Window {

        public HttpClient Client = new HttpClient();
        public List<Thread> Threads = new List<Thread>();
        public DateTime DiscordEpoch = new DateTime(2015, 1, 1);
        public bool ShowLocked = false;

        private readonly string[] LockEmoji = { "🔒" , "  \u2006\u2006\u2006\u2006\u2006" };

        public MainWindow() {
            InitializeComponent();
            Client.BaseAddress = new Uri("https://discord.com/api/v10/");
            Token.Password = Environment.GetEnvironmentVariable("THREAD_MANAGER_TOKEN");
            if(!(Token.Password == null || Token.Password == "")) RefreshButtonClick(null, null);
        }

        /// <summary>
        /// Handle the refresh button being clicked.
        /// </summary>
        private async void RefreshButtonClick(object sender, RoutedEventArgs e) {
            // Disable button
            RefreshButton.IsEnabled = false;

            //Clear and re-write
            Threads.Clear();
            ThreadList.Items.Clear();

            //Need to await Get Thread first
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", Token.Password.Trim());
            Threads.Clear();
            await GetThreadsAPICall($"guilds/{GuildId.Text}/threads/active", true);
            await GetThreadsAPICall($"channels/{ChannelId.Text}/threads/archived/public", false);
            WriteThreadsToListbox();

            // Enable button
            RefreshButton.IsEnabled = true;
        }

        /// <summary>
        /// Ask the Discord API for threads.
        /// </summary>
        private async Task GetThreadsAPICall(string apiPath, bool isActive) {
            try {

                // Call the API
                HttpResponseMessage resp = await Client.GetAsync(apiPath);
                if (!resp.IsSuccessStatusCode) throw new Exception("Discord API Error");

                // Get and parse response
                string respString = await resp.Content.ReadAsStringAsync();
                JObject respJSON = JObject.Parse(respString);
                JArray threads = (JArray) respJSON["threads"];

                // Add all threads to an object
                foreach (JObject thread in threads) {
                    if ((string) thread["parent_id"] == ChannelId.Text & (string) thread["flags"] != "2") {
                        Threads.Add(
                            new Thread(
                                (string) thread["id"],
                                (string) thread["name"],
                                (string) thread["last_message_id"],
                                isActive,
                                (bool) thread["thread_metadata"]["locked"]
                            )
                        );
                    }
                }
            } catch(Exception ex) {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// Change a thread neatly into a string.
        /// </summary>
        private string ThreadToString(Thread thread) {
            DateTime timeOfLastMessage = DiscordEpoch.AddMilliseconds((long) thread.LastMessageTime);
            TimeSpan timeSpan = DateTime.UtcNow - timeOfLastMessage;
            string time = timeSpan.Days > 3 ? $"{Math.Round(timeSpan.TotalDays, 1)} days" : $"{Math.Round(timeSpan.TotalHours, 1)} hours";
            string emojis = (thread.isLocked ? LockEmoji[0] : LockEmoji[1]) + (thread.isActive ? "🔴" : "❌");
            return $"{emojis} {thread.Name} ({time})";
        }

        /// <summary>
        /// Get a list of visible threads.
        /// </summary>
        private List<Thread> GetThreads() {
            List<Thread> localThreads = new List<Thread>();
            foreach (Thread thread in Threads) {
                if (ShowLocked == false && thread.isLocked) continue;
                localThreads.Add(thread);
            }
            return localThreads.OrderByDescending(a => a.LastMessageTime).ToList();
        }

        /// <summary>
        /// Get a thread with the given ID, returning both the index at which it appears in the main thread list, and the thread itself.
        /// </summary>
        private (int, Thread) GetSelectedThread() {
            Thread currentThread = GetThreads()[ThreadList.SelectedIndex];
            int index = 0;
            foreach (Thread thread in Threads) {
                if(thread.Id == currentThread.Id) return (index, thread);
                index++;
            }
            throw new Exception("Missing thread");
        }

        /// <summary>
        /// Clear and re-write the thread list.
        /// </summary>
        private void WriteThreadsToListbox() {
            ThreadList.Items.Clear();
            List<Thread> localThreads = GetThreads();
            foreach (Thread thread in localThreads) {
                ThreadList.Items.Add(ThreadToString(thread));
            }
        }

        /// <summary>
        /// Handle the archive button being pressed.
        /// </summary>
        private async void ArchiveButtonClick(object sender, RoutedEventArgs e) {
            // Get current thread
            (int index, Thread currentThread) = GetSelectedThread();

            // Perform API request
            HttpRequestMessage patchThread = new HttpRequestMessage(new HttpMethod("PATCH"), $"channels/{currentThread.Id}");
            patchThread.Content = new StringContent($"{{\"archived\":{currentThread.isActive}}}".ToLower(), Encoding.UTF8, "application/json");
            patchThread.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            try {
                HttpResponseMessage resp = await Client.SendAsync(patchThread);
                if (!resp.IsSuccessStatusCode){
                    throw new Exception($"Discord API Error - {resp.StatusCode} \n{await resp.Content.ReadAsStringAsync()}");
                }
                currentThread.isActive = !currentThread.isActive;
                Threads[index] = currentThread;
            }
            catch(Exception ex) {
                MessageBox.Show(ex.Message);
                return;
            }

            // Update shown threads
            int currentIndex = ThreadList.SelectedIndex;
            WriteThreadsToListbox();
            ThreadList.SelectedIndex = currentIndex;
        }

        /// <summary>
        /// Handle the lock button being pressed.
        /// </summary>
        private async void LockButtonClick(object sender, RoutedEventArgs e) {
            // Get current thread
            (int index, Thread currentThread) = GetSelectedThread();

            // Perform API request
            HttpRequestMessage patchThread = new HttpRequestMessage(new HttpMethod("PATCH"), $"channels/{currentThread.Id}");
            patchThread.Content = new StringContent($"{{\"locked\":{!currentThread.isLocked}, \"archived\": true}}".ToLower(), Encoding.UTF8, "application/json");
            patchThread.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            try {
                HttpResponseMessage resp = await Client.SendAsync(patchThread);
                if (!resp.IsSuccessStatusCode){
                    throw new Exception($"Discord API Error - {resp.StatusCode} \n{await resp.Content.ReadAsStringAsync()}");
                }
                currentThread.isActive = false;
                currentThread.isLocked = !currentThread.isLocked;
                Threads[index] = currentThread;
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message);
                return;
            }

            // Update shown threads
            int currentIndex = ThreadList.SelectedIndex;
            WriteThreadsToListbox();
            ThreadList.SelectedIndex = currentIndex;
        }

        /// <summary>
        /// Handle the thread open button being pressed.
        /// </summary>
        private void ThreadOpenButtonClick(object sender, RoutedEventArgs e) {
            (int index, Thread currentThread) = GetSelectedThread();
            Process.Start($"discord://-/channels/{GuildId.Text}/{ChannelId.Text}/threads/{currentThread.Id}");
        }

        /// <summary>
        /// Handle a thread item being double clicked.
        /// </summary>
        private void ThreadListItemClick(object sender, MouseButtonEventArgs e) {
            ThreadOpenButtonClick(sender, e);
        }

        /// <summary>
        /// Handle the "show locked threads" button being clicked.
        /// </summary>
        private void ShowLockedButtonClick(object sender, RoutedEventArgs e) {
            ShowLocked = !ShowLocked;
            WriteThreadsToListbox();
        }

        /// <summary>
        /// Delete a thread.
        /// </summary>
        private async void DeleteSelectedThread(object sender, EventArgs e) {
            (int index, Thread currentThread) = GetSelectedThread();
            HttpRequestMessage deleteThread = new HttpRequestMessage(new HttpMethod("DELETE"), $"channels/{currentThread.Id}");
            try {
                HttpResponseMessage resp = await Client.SendAsync(deleteThread);
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message);
                return;
            }
            Threads.Remove(currentThread);
            WriteThreadsToListbox();
        }

        /// <summary>
        /// Handle all button presses on the window.
        /// </summary>
        private void WindowKeyPress(object sender, KeyEventArgs e) {
            switch (e.Key) {
                case Key.Enter:
                    ThreadOpenButtonClick(sender, e);
                    break;
                case Key.O:
                    LockButtonClick(sender, e);
                    break;
                case Key.P:
                    ArchiveButtonClick(sender, e);
                    break;
                case Key.F5:
                    RefreshButtonClick(sender, e);
                    break;
                case Key.Delete:
                    DeleteSelectedThread(sender, e);
                    break;
                case Key.C:
                    if (Keyboard.Modifiers == ModifierKeys.Control) {
                        (int index, Thread currentThread) = GetSelectedThread();
                        String threadUrl = $"https://discord.com/channels/{GuildId.Text}/{ChannelId.Text}/threads/{currentThread.Id}";
                        Clipboard.SetText(threadUrl);
                    }
                    break;
            }
        }

    }
}
