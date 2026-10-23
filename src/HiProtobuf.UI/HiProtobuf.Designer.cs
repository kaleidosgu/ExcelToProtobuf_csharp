namespace HiProtobuf.UI
{
    partial class HiProtobuf
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.BtnChooseExprot = new System.Windows.Forms.Button();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.BtnChooseExcel = new System.Windows.Forms.Button();
            this.textBox5 = new System.Windows.Forms.TextBox();
            this.BtnChooseCsc = new System.Windows.Forms.Button();
            this.BtnExport = new System.Windows.Forms.Button();
            this.textBox6 = new System.Windows.Forms.TextBox();
            this.textBoxEnumProto = new System.Windows.Forms.TextBox();
            this.BtnChooseEnumProto = new System.Windows.Forms.Button();
            this.labelEnumProto = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // BtnChooseExprot
            // 
            this.BtnChooseExprot.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.BtnChooseExprot.Location = new System.Drawing.Point(665, 40);
            this.BtnChooseExprot.Name = "BtnChooseExprot";
            this.BtnChooseExprot.Size = new System.Drawing.Size(75, 23);
            this.BtnChooseExprot.TabIndex = 0;
            this.BtnChooseExprot.Text = "选择";
            this.BtnChooseExprot.UseVisualStyleBackColor = true;
            this.BtnChooseExprot.Click += new System.EventHandler(this._OnClickChooseExport);
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(37, 41);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(613, 21);
            this.textBox1.TabIndex = 1;
            this.textBox1.Text = "导出文件存放目录";
            this.textBox1.TextChanged += new System.EventHandler(this.textBox1_TextChanged);
            // 
            // textBox2
            // 
            this.textBox2.Location = new System.Drawing.Point(37, 86);
            this.textBox2.Name = "textBox2";
            this.textBox2.Size = new System.Drawing.Size(613, 21);
            this.textBox2.TabIndex = 2;
            this.textBox2.Text = "Excel存放目录";
            this.textBox2.TextChanged += new System.EventHandler(this.textBox2_TextChanged);
            // 
            // BtnChooseExcel
            // 
            this.BtnChooseExcel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.BtnChooseExcel.Location = new System.Drawing.Point(665, 86);
            this.BtnChooseExcel.Name = "BtnChooseExcel";
            this.BtnChooseExcel.Size = new System.Drawing.Size(75, 23);
            this.BtnChooseExcel.TabIndex = 3;
            this.BtnChooseExcel.Text = "选择";
            this.BtnChooseExcel.UseVisualStyleBackColor = true;
            this.BtnChooseExcel.Click += new System.EventHandler(this._OnClickChooseExcel);
            // 
            // textBox5
            // 
            this.textBox5.Location = new System.Drawing.Point(37, 139);
            this.textBox5.Name = "textBox5";
            this.textBox5.Size = new System.Drawing.Size(613, 21);
            this.textBox5.TabIndex = 6;
            this.textBox5.Text = ".Net编译器路径(\"C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe\")";
            this.textBox5.TextChanged += new System.EventHandler(this.textBox5_TextChanged);
            // 
            // BtnChooseCsc
            // 
            this.BtnChooseCsc.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.BtnChooseCsc.Location = new System.Drawing.Point(665, 139);
            this.BtnChooseCsc.Name = "BtnChooseCsc";
            this.BtnChooseCsc.Size = new System.Drawing.Size(75, 23);
            this.BtnChooseCsc.TabIndex = 9;
            this.BtnChooseCsc.Text = "选择";
            this.BtnChooseCsc.UseVisualStyleBackColor = true;
            this.BtnChooseCsc.Click += new System.EventHandler(this._OnCscClick);
            // 
            // BtnExport
            // 
            this.BtnExport.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.BtnExport.Location = new System.Drawing.Point(310, 226);
            this.BtnExport.Name = "BtnExport";
            this.BtnExport.Size = new System.Drawing.Size(160, 70);
            this.BtnExport.TabIndex = 10;
            this.BtnExport.Text = "导出";
            this.BtnExport.UseVisualStyleBackColor = true;
            this.BtnExport.Click += new System.EventHandler(this._OnExportClick);
            // 
            // textBox6
            // 
            this.textBox6.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.textBox6.Location = new System.Drawing.Point(37, 302);
            this.textBox6.Multiline = true;
            this.textBox6.Name = "textBox6";
            this.textBox6.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.textBox6.Size = new System.Drawing.Size(703, 370);
            this.textBox6.TabIndex = 11;
            this.textBox6.TextChanged += new System.EventHandler(this.textBox6_TextChanged);
            // textBoxEnumProto
            this.textBoxEnumProto.Location = new System.Drawing.Point(37, 181);
            this.textBoxEnumProto.Name = "textBoxEnumProto";
            this.textBoxEnumProto.Size = new System.Drawing.Size(613, 21);
            this.textBoxEnumProto.TabIndex = 12;
            this.textBoxEnumProto.TextChanged += new System.EventHandler(this.textBoxEnumProto_TextChanged);
            // BtnChooseEnumProto
            this.BtnChooseEnumProto.Location = new System.Drawing.Point(665, 181);
            this.BtnChooseEnumProto.Name = "BtnChooseEnumProto";
            this.BtnChooseEnumProto.Size = new System.Drawing.Size(75, 23);
            this.BtnChooseEnumProto.TabIndex = 13;
            this.BtnChooseEnumProto.Text = "选择";
            this.BtnChooseEnumProto.UseVisualStyleBackColor = true;
            this.BtnChooseEnumProto.Click += new System.EventHandler(this._OnClickChooseEnumProto);
            // labelEnumProto
            this.labelEnumProto.AutoSize = true;
            this.labelEnumProto.Location = new System.Drawing.Point(37, 166);
            this.labelEnumProto.Name = "labelEnumProto";
            this.labelEnumProto.Size = new System.Drawing.Size(173, 12);
            this.labelEnumProto.TabIndex = 14;
            this.labelEnumProto.Text = "枚举 Proto 源目录（按需配置）";
            // 
            // HiProtobuf
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 690);
            this.Controls.Add(this.BtnChooseEnumProto);
            this.Controls.Add(this.textBoxEnumProto);
            this.Controls.Add(this.labelEnumProto);
            this.Controls.Add(this.textBox6);
            this.Controls.Add(this.BtnExport);
            this.Controls.Add(this.BtnChooseCsc);
            this.Controls.Add(this.textBox5);
            this.Controls.Add(this.BtnChooseExcel);
            this.Controls.Add(this.textBox2);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.BtnChooseExprot);
            this.Name = "HiProtobuf";
            this.Text = "HiProtobuf";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button BtnChooseExprot;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.Button BtnChooseExcel;
        private System.Windows.Forms.TextBox textBox5;
        private System.Windows.Forms.Button BtnChooseCsc;
        private System.Windows.Forms.Button BtnExport;
        private System.Windows.Forms.TextBox textBox6;
        private System.Windows.Forms.TextBox textBoxEnumProto;
        private System.Windows.Forms.Button BtnChooseEnumProto;
        private System.Windows.Forms.Label labelEnumProto;
    }
}

