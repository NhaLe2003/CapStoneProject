using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using S7.Net;

namespace Machine
{
    public partial class Form1 : Form
    {
        private Plc _plc;

       
        public Form1()
        {
            InitializeComponent();
            InitPLC();
        }

        

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        //Connect to PLC
        private void InitPLC()
        {
            _plc = new Plc(CpuType.S71500,"192.168.1.201",0,1);
            try
            {
                _plc.Open();
                Console.WriteLine("Connect Successfull");
            }catch(Exception ex)
            {
                Console.WriteLine("can not connect to the PLc", ex.ToString());
            }
        }

        private void Update_timer_Tick(object sender, EventArgs e)
        {
            
        }

        private void btErrorBlowLine1_Click(object sender, EventArgs e)
        {
            _plc.Write("DB2.DBX382.0", true);
        }

        private void btRunBlowLine1_Click(object sender, EventArgs e)
        {
            _plc.Write("DB2.DBX382.0", false);
        }

        private void btErrorWashLine1_Click(object sender, EventArgs e)
        {
            _plc.Write("DB4.DBX382.0", true);
        }

        private void btRunWasheLine1_Click(object sender, EventArgs e)
        {
            _plc.Write("DB4.DBX382.0", false);
        }

        private void btErrorFillLine1_Click(object sender, EventArgs e)
        {
            _plc.Write("DB7.DBX382.0", true);
        }

        private void btRunFillLine1_Click(object sender, EventArgs e)
        {
            _plc.Write("DB7.DBX382.0", false);
        }

        private void btErrorCapperLine1_Click(object sender, EventArgs e)
        {
            _plc.Write("DB9.DBX382.0", true);
        }

        private void btRunCapperLine1_Click(object sender, EventArgs e)
        {
            _plc.Write("DB9.DBX382.0", false);
        }

        private void btErrorLabelLine1_Click(object sender, EventArgs e)
        {
            _plc.Write("DB11.DBX382.0", true);
        }

        private void btRunLabelLine1_Click(object sender, EventArgs e)
        {
            _plc.Write("DB11.DBX382.0", false);
        }

        private void btErrorPrintLine1_Click(object sender, EventArgs e)
        {
            _plc.Write("DB13.DBX382.0", true);
        }

        private void btRunPrintLine1_Click(object sender, EventArgs e)
        {
            _plc.Write("DB13.DBX382.0", false);
        }

        private void btErrorPackLine1_Click(object sender, EventArgs e)
        {
            _plc.Write("DB15.DBX382.0", true);
        }

        private void btRunPackLine1_Click(object sender, EventArgs e)
        {
            _plc.Write("DB15.DBX382.0", false);
        }
        //===Line 2===
        private void btErrorBlowLine2_Click(object sender, EventArgs e)
        {
            _plc.Write("DB21.DBX382.0", true);
        }

        private void btRunBlowLine2_Click(object sender, EventArgs e)
        {
            _plc.Write("DB21.DBX382.0", false);
        }

        private void btErrorWashLine2_Click(object sender, EventArgs e)
        {
            _plc.Write("DB23.DBX382.0", true);
        }

        private void btRunWashLine2_Click(object sender, EventArgs e)
        {
            _plc.Write("DB23.DBX382.0", false);
        }

        private void btErrorFillLine2_Click(object sender, EventArgs e)
        {
            _plc.Write("DB25.DBX382.0", true);
        }

        private void btRunFillLine2_Click(object sender, EventArgs e)
        {
            _plc.Write("DB25.DBX382.0", false);
        }

        private void btErrorCapperLine2_Click(object sender, EventArgs e)
        {
            _plc.Write("DB27.DBX382.0", true);
        }

        private void btRunCapperLine2_Click(object sender, EventArgs e)
        {
            _plc.Write("DB27.DBX382.0", false);
        }

        private void btErrorLabelLine2_Click(object sender, EventArgs e)
        {
            _plc.Write("DB29.DBX382.0", true);
        }

        private void btRunLabelLine2_Click(object sender, EventArgs e)
        {
            _plc.Write("DB29.DBX382.0", false);
        }

        private void btErrorPrintLine2_Click(object sender, EventArgs e)
        {
            _plc.Write("DB31.DBX382.0", true);
        }

        private void btRunPrintLine2_Click(object sender, EventArgs e)
        {
            _plc.Write("DB31.DBX382.0", false);
        }

        private void btErrorPackLine2_Click(object sender, EventArgs e)
        {
            _plc.Write("DB33.DBX382.0", true);
        }

        private void btRunPackLine2_Click(object sender, EventArgs e)
        {
            _plc.Write("DB33.DBX382.0", false);
        }
    }
}
