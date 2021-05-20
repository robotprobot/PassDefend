﻿using System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.ViewManagement;
using Windows.UI;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using System.Collections.Generic;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using System.Reflection;

namespace PassProtect
{
    /* TO DO:
     * Password time to live
     * Password generation
     */
    public partial class MainPage : Page
    {
        //prepare application files
        public StorageFolder localFolder = ApplicationData.Current.LocalFolder;

        //prepare basic variables
        public static string userpass { get; set; }
        public static bool passchangeactive = false;
        public bool newaccount = false;
        public int selectedID = 0;

        //this variable is to change the behaviour of the password change dialog when setting first password
        public static bool onboarding { get; set; }

        //prepare storage for the accounts
        internal List<DataAccess.AccountList> AccountData { get; set; }

        public MainPage()
        {
            //initialize the window
            this.InitializeComponent();

            //Hide the account viewer window so that the first thing the user will see after login is the "choose an account" message
            this.AccountDetailScroller.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

            //stylize the window to match the colour scheme of the application
            ColorScheme_CheckForScheme();

            //begin login process
            LoginSequence();
        }

        //function to change the colour and formatting of the title bar to match the rest of the application
        private void ModifyTitleBar(string colorValue)
        {
            //set colour for title bar
            var color = GetSolidColorBrush(colorValue).Color;
            //customise the title bar
            CoreApplicationViewTitleBar coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;

            //customise the exit, minimize and maximize buttons
            ApplicationViewTitleBar titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.BackgroundColor = color;
            titleBar.ButtonBackgroundColor = color;
            titleBar.ForegroundColor = Colors.White;
            titleBar.ButtonForegroundColor = Colors.White;
            titleBar.ButtonInactiveBackgroundColor = color;
            titleBar.ButtonInactiveForegroundColor = Colors.White;
        }

        //function to handle the login system
        private async void LoginSequence()
        {
            if (await localFolder.TryGetItemAsync("hash") != null) //if login file exists
            {
                //display the login dialog
                DisplayLoginDialog();
            }
            else //if login file does not exist
            {
                //prompt user to set password
                onboarding = true;
                ChangeMasterPassword();
            }
        }

        //function to run the login dialogue, begin creation of database, etc
        private async void DisplayLoginDialog()
        {
            bool notLoggedIn = true;
            //show the login dialog...
            PasswordPrompt signInDialog = new PasswordPrompt();
            while (notLoggedIn == true)
            {
                await signInDialog.ShowAsync();

                if (signInDialog.Result == PasswordPromptResult.SignInOK)
                {
                    //...sign in was successful
                    notLoggedIn = false;

                    //begin decryption process
                    //create the credential database if necessary, otherwise continue execution
                    await DataAccess.InitializeDatabase(userpass);

                    //first load of database accounts into account bar
                    RefreshAccounts();

                    //hide the login rectangle to show the main ui
                    //note, the login rectangle is NOT a security feature, it is simply to make the login dialog look nice. The data behind remains encrypted and unloaded until the password is confirmed.
                    fadeLoginBackground.Begin(); //begin the fade animation
                    fadeLoginBackground.Completed += (s, e) => //when the fade animation is completed...
                    {
                        loginRectangle.Width = 0; //...set the width and height of the now invisible box to 0 pixels so that it is out of the way and user can interact with the ui
                        loginRectangle.Height = 0;
                    };

                    //begin the login breach check
                    CheckForBreaches();
                }
                else if (signInDialog.Result == PasswordPromptResult.SignInCancel)
                {
                    //...sign in was cancelled by the user, exiting application
                    CoreApplication.Exit();
                }
            }
        }

        //function to refresh the accounts from database
        private void RefreshAccounts()
        {
            //if account bar is not empty, clear it
            if (AccountData != null)
            {
                AccountData.Clear();
            }
            //request account data from database
            AccountData = DataAccess.GetAccountData(userpass);
            AccountData.RemoveAt(0);
            //display account data in the account bar
            accountList.ItemsSource = AccountData;
        }

        //function to check all passwords against password breach databases
        private async void CheckForBreaches()
        {
            timeSinceBreachText.Text = "Checking for password breaches...";
            refreshBreachCheckButton.Content = "...";
            foreach (var account in AccountData) { //for each account in account data...
                bool passCheck = await BreachCheck.checkPassword(account.Password); //check the password against the API
                if (passCheck == true) //if the password is found in the breach list...
                {
                    ContentDialog deleteConfirmDialog = new ContentDialog
                    {
                        Title = "Breached password identified",
                        Content = "The password to your '" + account.Name + "' account appeared in an online breach, please change it.",
                        PrimaryButtonText = "Okay"
                    };
                    ContentDialogResult result = await deleteConfirmDialog.ShowAsync();
                }
            }
            timeSinceBreachText.Text = "Last password breach check: " + DateTime.Now;
            refreshBreachCheckButton.Content = "Refresh";
        }

        //function to complete the changing of the master password
        private async void ChangeMasterPassword()
        {
            passchangeactive = true;
            bool dialogNotCompleted = true;
            //show the change password dialog...
            PasswordCreation changePasswordDialog = new PasswordCreation();
            while (dialogNotCompleted == true)
            {
                await changePasswordDialog.ShowAsync();

                if (changePasswordDialog.Result == PasswordChangeResult.PassChangeOK)
                {
                    dialogNotCompleted = false; //breaking loop because password change completed
                    if (onboarding == true)
                    {
                        onboarding = false; //if onboarding is active, turning it off
                        DisplayLoginDialog(); //ask user for the password, for the first time
                    }
                }
                else if (changePasswordDialog.Result == PasswordChangeResult.PassChangeCancel && onboarding == false)
                {
                    dialogNotCompleted = false; //breaking loop to cancel change request
                }
                else if (changePasswordDialog.Result == PasswordChangeResult.PassChangeCancel && onboarding == true)
                {
                    CoreApplication.Exit(); //exiting application because onboarding process has been cancelled
                }
            }
            passchangeactive = false;
        }

        //function to convert Hex codes to Solid brushes, for more precise colour options
        public SolidColorBrush GetSolidColorBrush(string hex)
        {
            hex = hex.Replace("#", string.Empty);
            byte a = (byte)(Convert.ToUInt32(hex.Substring(0, 2), 16));
            byte r = (byte)(Convert.ToUInt32(hex.Substring(2, 2), 16));
            byte g = (byte)(Convert.ToUInt32(hex.Substring(4, 2), 16));
            byte b = (byte)(Convert.ToUInt32(hex.Substring(6, 2), 16));
            SolidColorBrush myBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(a, r, g, b));
            return myBrush;
        }

        //function to take searchbox content and filter out the account list
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var cont = from s in AccountData where s.Name.Contains(SearchBox.Text, StringComparison.CurrentCultureIgnoreCase) select s;
            accountList.ItemsSource = cont;
        }

        //add account button
        private void addAccountButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            //Deselect account list item if listed
            accountList.SelectedItem = null;
            newaccount = true;
            deleteEntryButton.IsEnabled = false;

            //clear out the account panel information if filled
            accountNameTextBox.Text = "";
            usernameTextBox.Text = "";
            emailTextBox.Text = "";
            passwordTextBox.Text = "";
            notesTextBox.Text = "";

            //bring up the account panel if not already up
            this.AccountDetailScroller.Visibility = Windows.UI.Xaml.Visibility.Visible;
        }

        //account selected
        private void accountList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            newaccount = false;
            saveButton.IsEnabled = false;
            revertButton.IsEnabled = false;
            deleteEntryButton.IsEnabled = true;
            if (accountList.SelectedIndex != -1)
            {
                //get the ID of the requested content
                object info = accountList.SelectedValue;
                Type type = info.GetType();
                IList<PropertyInfo> props = new List<PropertyInfo>(type.GetProperties());
                foreach (PropertyInfo prop in props)
                {
                    object propValue = prop.GetValue(info, null);

                    if (prop.Name == "ID")
                    {
                        selectedID = Int32.Parse((string)propValue);
                    }
                }

                //Fill the data
                foreach (var item in AccountData)
                {
                    if (Int32.Parse(item.ID) == selectedID)
                    {
                        accountNameHeader.Text = "Your " + item.Name + " account";
                        accountNameTextBox.Text = item.Name;
                        usernameTextBox.Text = item.Username;
                        emailTextBox.Text = item.Email;
                        passwordTextBox.Text = item.Password;
                        notesTextBox.Text = item.Notes;
                    }
                }
            }

            //bring up the account panel if not already up
            this.AccountDetailScroller.Visibility = Windows.UI.Xaml.Visibility.Visible;
        }

        //function to copy username to clipboard upon button press
        private void copyUsernameButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(usernameTextBox.Text);
            Clipboard.SetContent(dataPackage);
        }

        //function to copy username to clipboard upon button press
        private void copyEmailButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(emailTextBox.Text);
            Clipboard.SetContent(dataPackage);
        }

        //function to copy username to clipboard upon button press
        private void copyPasswordButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(passwordTextBox.Text);
            Clipboard.SetContent(dataPackage);
        }

        //save the account button
        private void saveButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (newaccount == true)
            {
                //add new account entry to database
                DataAccess.AddData(userpass, accountNameTextBox.Text, emailTextBox.Text, usernameTextBox.Text, passwordTextBox.Text, notesTextBox.Text);
                RefreshAccounts();
                saveButton.IsEnabled = false;
                revertButton.IsEnabled = false;
                accountList.SelectedIndex = accountList.Items.Count - 1;
            }
            else
            {
                //update the account entry in the database
                DataAccess.UpdateData(userpass, selectedID, accountNameTextBox.Text, emailTextBox.Text, usernameTextBox.Text, passwordTextBox.Text, notesTextBox.Text);
                RefreshAccounts();
                saveButton.IsEnabled = false;
                revertButton.IsEnabled = false;
            }
        }

        //change title of account window as textbox changes
        private void accountNameTextbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            //if textbox is changed...
            if (accountNameTextBox.Text.Length == 0) //...and textbox is empty...
            {
                //...set text to default
                accountNameHeader.Text = "New account";
            }
            else //...or textbox is not empty...
            {
                //...set text to the textbox content
                accountNameHeader.Text = "Your " + accountNameTextBox.Text + " account";
            }

            if (accountNameTextBox.Text.Length > 0 && (usernameTextBox.Text.Length > 0 || emailTextBox.Text.Length > 0) && passwordTextBox.Text.Length > 0)
            {
                saveButton.IsEnabled = true;
                if (newaccount == false)
                {
                    revertButton.IsEnabled = true;
                }
            }
            else
            {
                saveButton.IsEnabled = false;
                revertButton.IsEnabled = false;
            }
        }

        //function to change the header text with the account name
        private void usernameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (accountNameTextBox.Text.Length > 0 && (usernameTextBox.Text.Length > 0 || emailTextBox.Text.Length > 0) && passwordTextBox.Text.Length > 0)
            {
                saveButton.IsEnabled = true;
                if (newaccount == false)
                {
                    revertButton.IsEnabled = true;
                }
            }
            else
            {
                saveButton.IsEnabled = false;
                revertButton.IsEnabled = false;
            }
        }

        //handling the allowance of the save button being used
        private void emailTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (accountNameTextBox.Text.Length > 0 && (usernameTextBox.Text.Length > 0 || emailTextBox.Text.Length > 0) && passwordTextBox.Text.Length > 0)
            {
                saveButton.IsEnabled = true;
                if (newaccount == false)
                {
                    revertButton.IsEnabled = true;
                }
            }
            else
            {
                saveButton.IsEnabled = false;
                revertButton.IsEnabled = false;
            }
        }

        //handling the allowance of the save button being used
        private void notesTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (accountNameTextBox.Text.Length > 0 && (usernameTextBox.Text.Length > 0 || emailTextBox.Text.Length > 0) && passwordTextBox.Text.Length > 0)
            {
                saveButton.IsEnabled = true;
                if (newaccount == false)
                {
                    revertButton.IsEnabled = true;
                }
            }
            else
            {
                saveButton.IsEnabled = false;
                revertButton.IsEnabled = false;
            }
        }

        //handling the allowance of the save button being used
        private void passwordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (accountNameTextBox.Text.Length > 0 && (usernameTextBox.Text.Length > 0 || emailTextBox.Text.Length > 0) && passwordTextBox.Text.Length > 0)
            {
                saveButton.IsEnabled = true;
                if (newaccount == false)
                {
                    revertButton.IsEnabled = true;
                }
            }
            else
            {
                saveButton.IsEnabled = false;
                revertButton.IsEnabled = false;
            }
        }

        //handling the reverting of details when clicked
        private void revertButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            foreach (var item in AccountData)
            {
                if (Int32.Parse(item.ID) == selectedID)
                {
                    accountNameHeader.Text = "Your " + item.Name + " account";
                    accountNameTextBox.Text = item.Name;
                    usernameTextBox.Text = item.Username;
                    emailTextBox.Text = item.Email;
                    passwordTextBox.Text = item.Password;
                    notesTextBox.Text = item.Notes;
                }
            }
            saveButton.IsEnabled = false;
            revertButton.IsEnabled = false;
        }

        //handling of deleting entry when clicked
        private async void deleteEntryButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            ContentDialog deleteConfirmDialog = new ContentDialog
            {
                Title = "Are you sure?",
                Content = "You are about to delete an account. \r\nThis cannot be undone!",
                PrimaryButtonText = "Delete",
                SecondaryButtonText = "Cancel"
            };
            ContentDialogResult result = await deleteConfirmDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                DataAccess.DeleteData(userpass, selectedID);
                RefreshAccounts();
                accountList.SelectedIndex = -1;
                this.AccountDetailScroller.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                deleteEntryButton.IsEnabled = false;
            }
        }

        //handling of refresh breach check button
        private void refreshBreachCheckButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            CheckForBreaches();
        }

        private void MenuFlyoutItem_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            //change master password
            ChangeMasterPassword();
        }

        private async void ColorScheme_CheckForScheme()
        {
            if (await localFolder.TryGetItemAsync("colorScheme") != null) //if colorscheme file exists
            {
                //set scheme
                StorageFile colorschemefile = await localFolder.GetFileAsync("colorScheme");
                string fileContent = await FileIO.ReadTextAsync(colorschemefile);
                if (fileContent == "green")
                {
                    ColorScheme_Green();
                }
                else if (fileContent == "red")
                {
                    ColorScheme_Red();
                }
                else if (fileContent == "purple")
                {
                    ColorScheme_Purple();
                }
                else if (fileContent == "black")
                {
                    ColorScheme_Black();
                }
                else
                {
                    ColorScheme_Green();
                }
            }
            else //if colorscheme file does not exist
            {
                ColorScheme_Green();
            }
        }

        private async void MenuFlyoutItem_Click_1(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            //change colour green
            ColorScheme_Green();
            StorageFile colorschemefile = await localFolder.CreateFileAsync("colorScheme", CreationCollisionOption.OpenIfExists);
            await FileIO.WriteTextAsync(colorschemefile, "green");
        }

        private async void MenuFlyoutItem_Click_2(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            //change colour red
            ColorScheme_Red();
            StorageFile colorschemefile = await localFolder.CreateFileAsync("colorScheme", CreationCollisionOption.OpenIfExists);
            await FileIO.WriteTextAsync(colorschemefile, "red");
        }

        private async void MenuFlyoutItem_Click_3(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            //change colour purple
            ColorScheme_Purple();
            StorageFile colorschemefile = await localFolder.CreateFileAsync("colorScheme", CreationCollisionOption.OpenIfExists);
            await FileIO.WriteTextAsync(colorschemefile, "purple");
        }

        private async void MenuFlyoutItem_Click_4(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            //change colour black
            ColorScheme_Black();
            StorageFile colorschemefile = await localFolder.CreateFileAsync("colorScheme", CreationCollisionOption.OpenIfExists);
            await FileIO.WriteTextAsync(colorschemefile, "black");
        }

        private void ColorScheme_Green()
        {
            AccountDetailWindow.Background = GetSolidColorBrush("FF165D43");
            AccountWindowSpacer.Background = GetSolidColorBrush("FF165D43");
            NoAccountWindow.Background = GetSolidColorBrush("FF165D43");
            OptionBar.Background = GetSolidColorBrush("FF20805C");
            StatusBar.Background = GetSolidColorBrush("FF26956C");
            SideBar.Background = GetSolidColorBrush("FF26956C");
            accountList.Background = GetSolidColorBrush("FF739E8E");
            loginRectangle.Fill = GetSolidColorBrush("FF165D43");
            ModifyTitleBar("FF165D43");
        }
        private void ColorScheme_Red()
        {
            AccountDetailWindow.Background = GetSolidColorBrush("FF5D1616");
            AccountWindowSpacer.Background = GetSolidColorBrush("FF5D1616");
            NoAccountWindow.Background = GetSolidColorBrush("FF5D1616");
            OptionBar.Background = GetSolidColorBrush("FF802020");
            StatusBar.Background = GetSolidColorBrush("FF952626");
            SideBar.Background = GetSolidColorBrush("FF952626");
            accountList.Background = GetSolidColorBrush("FF9E7373");
            loginRectangle.Fill = GetSolidColorBrush("FF5D1616");
            ModifyTitleBar("FF5D1616");
        }
        private void ColorScheme_Purple()
        {
            AccountDetailWindow.Background = GetSolidColorBrush("FF40165D");
            AccountWindowSpacer.Background = GetSolidColorBrush("FF40165D");
            NoAccountWindow.Background = GetSolidColorBrush("FF40165D");
            OptionBar.Background = GetSolidColorBrush("FF4C2080");
            StatusBar.Background = GetSolidColorBrush("FF6D2695");
            SideBar.Background = GetSolidColorBrush("FF6D2695");
            accountList.Background = GetSolidColorBrush("FF81739E");
            loginRectangle.Fill = GetSolidColorBrush("FF40165D");
            ModifyTitleBar("FF40165D");
        }
        private void ColorScheme_Black()
        {
            AccountDetailWindow.Background = GetSolidColorBrush("FF1B1B1B");
            AccountWindowSpacer.Background = GetSolidColorBrush("FF1B1B1B");
            NoAccountWindow.Background = GetSolidColorBrush("FF1B1B1B");
            OptionBar.Background = GetSolidColorBrush("FF171717");
            StatusBar.Background = GetSolidColorBrush("FF0F0F0F");
            SideBar.Background = GetSolidColorBrush("FF0F0F0F");
            accountList.Background = GetSolidColorBrush("FF2E2E2E");
            loginRectangle.Fill = GetSolidColorBrush("FF1B1B1B");
            ModifyTitleBar("FF1B1B1B");
        }

        private void MenuFlyoutItem_Click_5(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            //Export database button
            ImportExportEngine.ExportDB();
        }
    }
}