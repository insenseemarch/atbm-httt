using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

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
                        using (var mainForm = new Form1(loginForm.AuthenticatedService))
                        {
                            if (mainForm.ShowDialog() == DialogResult.Cancel)
                            {
                                // User clicked logout
                                continueLoop = true;
                            }
                            else
                            {
                                continueLoop = false;
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
