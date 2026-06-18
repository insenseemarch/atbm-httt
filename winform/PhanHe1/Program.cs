using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PhanHe1.Forms;

namespace PhanHe1
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool continueLoop = true;
            while (continueLoop)
            {
                using (var loginForm = new LoginForm())
                {
                    if (loginForm.ShowDialog() == DialogResult.OK)
                    {
                    // Routing theo lựa chọn trên LoginForm (SelectedPhase = 1 hoặc 2)
                    if (loginForm.SelectedPhase == 1)
                    {
                        using (var mainForm = new Form1(loginForm.AuthenticatedService))
                        {
                            continueLoop = mainForm.ShowDialog() == DialogResult.Cancel;
                        }
                    }
                    else
                    {
                        using (var userForm = new SubSystem2Form(loginForm.AuthenticatedService))
                        {
                            userForm.FormClosing += (s, e) =>
                            {
                                if (userForm.DialogResult == DialogResult.None)
                                    userForm.DialogResult = DialogResult.Cancel;
                            };
                            continueLoop = userForm.ShowDialog() == DialogResult.Cancel;
                        }
                    }
                    }
                    else
                    {
                        continueLoop = false;
                    }
                }
            }
        }
    }
}
