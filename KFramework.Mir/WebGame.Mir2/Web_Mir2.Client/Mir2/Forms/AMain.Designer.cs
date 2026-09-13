using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using MirEngine;

namespace Launcher
{
    partial class AMain
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        
        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AMain));
            this.ActionLabel = new Label();
            this.SpeedLabel = new Label();
            this.InterfaceTimer = new Timer(this.components);
            this.Movement_panel = new Panel();
            this.Name_label = new Label();
            this.pictureBox1 = new PictureBox();
            this.Close_pb = new PictureBox();
            this.Config_pb = new PictureBox();
            this.Version_label = new Label();
            this.CurrentFile_label = new Label();
            this.CurrentPercent_label = new Label();
            this.TotalPercent_label = new Label();
            this.Credit_label = new Label();
            this.ProgTotalEnd_pb = new PictureBox();
            this.ProgEnd_pb = new PictureBox();
            this.ProgressCurrent_pb = new PictureBox();
            this.TotalProg_pb = new PictureBox();
            this.Launch_pb = new PictureBox();
            this.Main_browser = new Microsoft.Web.WebView2.WinForms.WebView2();
            this.Movement_panel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.Close_pb)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.Config_pb)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ProgTotalEnd_pb)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ProgEnd_pb)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ProgressCurrent_pb)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.TotalProg_pb)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.Launch_pb)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.Main_browser)).BeginInit();
            this.SuspendLayout();
            // 
            // ActionLabel
            // 
            this.ActionLabel.Anchor = AnchorStyles.Bottom;
            this.ActionLabel.BackColor = Color.Transparent;
            this.ActionLabel.Font = new Font("Calibri", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            this.ActionLabel.ForeColor = Color.Gray;
            this.ActionLabel.Location = new Point(478, 470);
            this.ActionLabel.Margin = new Padding(4, 0, 4, 0);
            this.ActionLabel.Name = "ActionLabel";
            this.ActionLabel.Size = new Size(126, 21);
            this.ActionLabel.TabIndex = 4;
            this.ActionLabel.Text = "1423MB/2000MB";
            this.ActionLabel.TextAlign = ContentAlignment.MiddleRight;
            this.ActionLabel.Visible = false;
            this.ActionLabel.Click += new System.EventHandler(this.ActionLabel_Click);
            // 
            // SpeedLabel
            // 
            this.SpeedLabel.Anchor = AnchorStyles.Bottom;
            this.SpeedLabel.BackColor = Color.Transparent;
            this.SpeedLabel.Font = new Font("Calibri", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            this.SpeedLabel.ForeColor = Color.Gray;
            this.SpeedLabel.Location = new Point(341, 474);
            this.SpeedLabel.Margin = new Padding(4, 0, 4, 0);
            this.SpeedLabel.Name = "SpeedLabel";
            this.SpeedLabel.RightToLeft = RightToLeft.No;
            this.SpeedLabel.Size = new Size(83, 18);
            this.SpeedLabel.TabIndex = 13;
            this.SpeedLabel.Text = "Speed";
            this.SpeedLabel.TextAlign = ContentAlignment.TopRight;
            this.SpeedLabel.Visible = false;
            // 
            // InterfaceTimer
            // 
            this.InterfaceTimer.Enabled = true;
            this.InterfaceTimer.Interval = 50;
            this.InterfaceTimer.Tick += new System.EventHandler(this.InterfaceTimer_Tick);
            // 
            // Movement_panel
            // 
            this.Movement_panel.BackColor = Color.Transparent;
            this.Movement_panel.BackgroundImageLayout = ImageLayout.Center;
            this.Movement_panel.Controls.Add(this.Name_label);
            this.Movement_panel.Controls.Add(this.pictureBox1);
            this.Movement_panel.Controls.Add(this.Close_pb);
            this.Movement_panel.Controls.Add(this.Config_pb);
            this.Movement_panel.Location = new Point(14, 6);
            this.Movement_panel.Margin = new Padding(4, 3, 4, 3);
            this.Movement_panel.Name = "Movement_panel";
            this.Movement_panel.Size = new Size(922, 43);
            this.Movement_panel.TabIndex = 21;
            this.Movement_panel.MouseClick += new MouseEventHandler(this.Movement_panel_MouseClick);
            this.Movement_panel.MouseDown += new MouseEventHandler(this.Movement_panel_MouseClick);
            this.Movement_panel.MouseMove += new MouseEventHandler(this.Movement_panel_MouseMove);
            this.Movement_panel.MouseUp += new MouseEventHandler(this.Movement_panel_MouseUp);
            // 
            // Name_label
            // 
            this.Name_label.BackColor = Color.Transparent;
            this.Name_label.Font = new Font("Microsoft Sans Serif", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            this.Name_label.ForeColor = Color.White;
            this.Name_label.Image = global::Client.Resources.Images.server_base;
            this.Name_label.Location = new Point(289, 11);
            this.Name_label.Margin = new Padding(4, 0, 4, 0);
            this.Name_label.Name = "Name_label";
            this.Name_label.Size = new Size(217, 25);
            this.Name_label.TabIndex = 0;
            this.Name_label.Text = "Crystal Mir 2";
            this.Name_label.TextAlign = ContentAlignment.MiddleCenter;
            this.Name_label.Visible = false;
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = global::Client.Resources.Images.server_base;
            this.pictureBox1.Location = new Point(358, -46);
            this.pictureBox1.Margin = new Padding(4, 3, 4, 3);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new Size(217, 23);
            this.pictureBox1.TabIndex = 33;
            this.pictureBox1.TabStop = false;
            // 
            // Close_pb
            // 
            this.Close_pb.BackColor = Color.Transparent;
            this.Close_pb.BackgroundImageLayout = ImageLayout.Center;
            this.Close_pb.Image = global::Client.Resources.Images.Cross_Base;
            this.Close_pb.Location = new Point(757, 11);
            this.Close_pb.Margin = new Padding(4, 3, 4, 3);
            this.Close_pb.Name = "Close_pb";
            this.Close_pb.Size = new Size(22, 23);
            this.Close_pb.TabIndex = 20;
            this.Close_pb.TabStop = false;
            this.Close_pb.Click += new System.EventHandler(this.Close_pb_Click);
            this.Close_pb.MouseDown += new MouseEventHandler(this.Close_pb_MouseDown);
            this.Close_pb.MouseEnter += new System.EventHandler(this.Close_pb_MouseEnter);
            this.Close_pb.MouseLeave += new System.EventHandler(this.Close_pb_MouseLeave);
            this.Close_pb.MouseUp += new MouseEventHandler(this.Close_pb_MouseUp);
            // 
            // Config_pb
            // 
            this.Config_pb.BackColor = Color.Transparent;
            this.Config_pb.BackgroundImageLayout = ImageLayout.Center;
            this.Config_pb.Image = global::Client.Resources.Images.Config_Base;
            this.Config_pb.Location = new Point(732, 11);
            this.Config_pb.Margin = new Padding(4, 3, 4, 3);
            this.Config_pb.Name = "Config_pb";
            this.Config_pb.Size = new Size(22, 23);
            this.Config_pb.TabIndex = 32;
            this.Config_pb.TabStop = false;
            this.Config_pb.Click += new System.EventHandler(this.Config_pb_Click);
            this.Config_pb.MouseDown += new MouseEventHandler(this.Config_pb_MouseDown);
            this.Config_pb.MouseEnter += new System.EventHandler(this.Config_pb_MouseEnter);
            this.Config_pb.MouseLeave += new System.EventHandler(this.Config_pb_MouseLeave);
            this.Config_pb.MouseUp += new MouseEventHandler(this.Config_pb_MouseUp);
            // 
            // Version_label
            // 
            this.Version_label.Anchor = AnchorStyles.Bottom;
            this.Version_label.BackColor = Color.Transparent;
            this.Version_label.Font = new Font("Calibri", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            this.Version_label.ForeColor = Color.Gray;
            this.Version_label.Location = new Point(648, 534);
            this.Version_label.Margin = new Padding(4, 0, 4, 0);
            this.Version_label.Name = "Version_label";
            this.Version_label.Size = new Size(143, 15);
            this.Version_label.TabIndex = 31;
            this.Version_label.Text = "Version 1.0.0.0";
            this.Version_label.TextAlign = ContentAlignment.TopRight;
            // 
            // CurrentFile_label
            // 
            this.CurrentFile_label.Anchor = AnchorStyles.Bottom;
            this.CurrentFile_label.BackColor = Color.Transparent;
            this.CurrentFile_label.Font = new Font("Calibri", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            this.CurrentFile_label.ForeColor = Color.Gray;
            this.CurrentFile_label.Location = new Point(62, 470);
            this.CurrentFile_label.Margin = new Padding(4, 0, 4, 0);
            this.CurrentFile_label.Name = "CurrentFile_label";
            this.CurrentFile_label.Size = new Size(422, 20);
            this.CurrentFile_label.TabIndex = 27;
            this.CurrentFile_label.Text = "Checking Files.";
            this.CurrentFile_label.TextAlign = ContentAlignment.MiddleLeft;
            this.CurrentFile_label.Visible = false;
            // 
            // CurrentPercent_label
            // 
            this.CurrentPercent_label.Anchor = AnchorStyles.Bottom;
            this.CurrentPercent_label.BackColor = Color.Transparent;
            this.CurrentPercent_label.Font = new Font("Calibri", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            this.CurrentPercent_label.ForeColor = Color.Gray;
            this.CurrentPercent_label.Location = new Point(619, 488);
            this.CurrentPercent_label.Margin = new Padding(4, 0, 4, 0);
            this.CurrentPercent_label.Name = "CurrentPercent_label";
            this.CurrentPercent_label.Size = new Size(41, 23);
            this.CurrentPercent_label.TabIndex = 28;
            this.CurrentPercent_label.Text = "100%";
            this.CurrentPercent_label.TextAlign = ContentAlignment.MiddleCenter;
            this.CurrentPercent_label.Visible = false;
            // 
            // TotalPercent_label
            // 
            this.TotalPercent_label.Anchor = AnchorStyles.Bottom;
            this.TotalPercent_label.BackColor = Color.Transparent;
            this.TotalPercent_label.Font = new Font("Calibri", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            this.TotalPercent_label.ForeColor = Color.Gray;
            this.TotalPercent_label.Location = new Point(619, 509);
            this.TotalPercent_label.Margin = new Padding(4, 0, 4, 0);
            this.TotalPercent_label.Name = "TotalPercent_label";
            this.TotalPercent_label.Size = new Size(41, 23);
            this.TotalPercent_label.TabIndex = 29;
            this.TotalPercent_label.Text = "100%";
            this.TotalPercent_label.TextAlign = ContentAlignment.MiddleCenter;
            this.TotalPercent_label.Visible = false;
            // 
            // Credit_label
            // 
            this.Credit_label.Anchor = AnchorStyles.Bottom;
            this.Credit_label.AutoSize = true;
            this.Credit_label.BackColor = Color.Transparent;
            this.Credit_label.Font = new Font("Calibri", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            this.Credit_label.ForeColor = Color.Gray;
            this.Credit_label.Location = new Point(13, 534);
            this.Credit_label.Margin = new Padding(4, 0, 4, 0);
            this.Credit_label.Name = "Credit_label";
            this.Credit_label.Size = new Size(114, 13);
            this.Credit_label.TabIndex = 30;
            this.Credit_label.Text = "Powered by Crystal M2";
            this.Credit_label.Click += new System.EventHandler(this.Credit_label_Click);
            // 
            // ProgTotalEnd_pb
            // 
            this.ProgTotalEnd_pb.Anchor = AnchorStyles.None;
            this.ProgTotalEnd_pb.BackColor = Color.Transparent;
            this.ProgTotalEnd_pb.BackgroundImageLayout = ImageLayout.Center;
            this.ProgTotalEnd_pb.Image = global::Client.Resources.Images.NEW_Progress_End__Blue_;
            this.ProgTotalEnd_pb.Location = new Point(599, 529);
            this.ProgTotalEnd_pb.Margin = new Padding(4, 3, 4, 3);
            this.ProgTotalEnd_pb.Name = "ProgTotalEnd_pb";
            this.ProgTotalEnd_pb.Size = new Size(5, 17);
            this.ProgTotalEnd_pb.TabIndex = 26;
            this.ProgTotalEnd_pb.TabStop = false;
            // 
            // ProgEnd_pb
            // 
            this.ProgEnd_pb.Anchor = AnchorStyles.None;
            this.ProgEnd_pb.BackColor = Color.Transparent;
            this.ProgEnd_pb.BackgroundImageLayout = ImageLayout.Center;
            this.ProgEnd_pb.Image = global::Client.Resources.Images.NEW_Progress_End__Green_;
            this.ProgEnd_pb.Location = new Point(535, 529);
            this.ProgEnd_pb.Margin = new Padding(4, 3, 4, 3);
            this.ProgEnd_pb.Name = "ProgEnd_pb";
            this.ProgEnd_pb.Size = new Size(5, 17);
            this.ProgEnd_pb.TabIndex = 25;
            this.ProgEnd_pb.TabStop = false;
            // 
            // ProgressCurrent_pb
            // 
            this.ProgressCurrent_pb.Anchor = AnchorStyles.None;
            this.ProgressCurrent_pb.BackColor = Color.Transparent;
            this.ProgressCurrent_pb.BackgroundImageLayout = ImageLayout.Center;
            this.ProgressCurrent_pb.Image = global::Client.Resources.Images.Green_Progress;
            this.ProgressCurrent_pb.Location = new Point(60, 494);
            this.ProgressCurrent_pb.Margin = new Padding(4, 3, 4, 3);
            this.ProgressCurrent_pb.Name = "ProgressCurrent_pb";
            this.ProgressCurrent_pb.Size = new Size(557, 17);
            this.ProgressCurrent_pb.TabIndex = 23;
            this.ProgressCurrent_pb.TabStop = false;
            this.ProgressCurrent_pb.SizeChanged += new System.EventHandler(this.ProgressCurrent_pb_SizeChanged);
            // 
            // TotalProg_pb
            // 
            this.TotalProg_pb.Anchor = AnchorStyles.None;
            this.TotalProg_pb.BackColor = Color.Transparent;
            this.TotalProg_pb.BackgroundImageLayout = ImageLayout.Center;
            this.TotalProg_pb.Image = global::Client.Resources.Images.Blue_Progress;
            this.TotalProg_pb.Location = new Point(60, 511);
            this.TotalProg_pb.Margin = new Padding(4, 3, 4, 3);
            this.TotalProg_pb.Name = "TotalProg_pb";
            this.TotalProg_pb.Size = new Size(558, 16);
            this.TotalProg_pb.TabIndex = 22;
            this.TotalProg_pb.TabStop = false;
            this.TotalProg_pb.SizeChanged += new System.EventHandler(this.TotalProg_pb_SizeChanged);
            // 
            // Launch_pb
            // 
            this.Launch_pb.Anchor = AnchorStyles.Bottom;
            this.Launch_pb.BackColor = Color.Transparent;
            this.Launch_pb.BackgroundImageLayout = ImageLayout.Stretch;
            this.Launch_pb.Cursor = Cursors.Hand;
            this.Launch_pb.Image = global::Client.Resources.Images.Launch_Base1;
            this.Launch_pb.Location = new Point(660, 476);
            this.Launch_pb.Margin = new Padding(4, 3, 4, 3);
            this.Launch_pb.Name = "Launch_pb";
            this.Launch_pb.Size = new Size(131, 56);
            this.Launch_pb.TabIndex = 19;
            this.Launch_pb.TabStop = false;
            this.Launch_pb.Click += new System.EventHandler(this.Launch_pb_Click);
            this.Launch_pb.MouseDown += new MouseEventHandler(this.Launch_pb_MouseDown);
            this.Launch_pb.MouseEnter += new System.EventHandler(this.Launch_pb_MouseEnter);
            this.Launch_pb.MouseLeave += new System.EventHandler(this.Launch_pb_MouseLeave);
            this.Launch_pb.MouseUp += new MouseEventHandler(this.Launch_pb_MouseUp);
            // 
            // Main_browser
            // 
            this.Main_browser.AllowExternalDrop = true;
            this.Main_browser.CausesValidation = false;
            this.Main_browser.CreationProperties = null;
            this.Main_browser.DefaultBackgroundColor = Color.White;
            this.Main_browser.Location = new Point(11, 53);
            this.Main_browser.Margin = new Padding(4, 3, 4, 3);
            this.Main_browser.MaximumSize = new Size(782, 403);
            this.Main_browser.Name = "Main_browser";
            this.Main_browser.Size = new Size(782, 403);
            this.Main_browser.TabIndex = 32;
            this.Main_browser.Visible = false;
            this.Main_browser.ZoomFactor = 1D;
            // 
            // AMain
            // 
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.BackColor = SystemColors.ActiveCaption;
            this.BackgroundImage = global::Client.Resources.Images.pfffft;
            this.BackgroundImageLayout = ImageLayout.Center;
            this.ClientSize = new Size(804, 558);
            this.Controls.Add(this.Main_browser);
            this.Controls.Add(this.SpeedLabel);
            this.Controls.Add(this.Credit_label);
            this.Controls.Add(this.Version_label);
            this.Controls.Add(this.TotalPercent_label);
            this.Controls.Add(this.CurrentPercent_label);
            this.Controls.Add(this.CurrentFile_label);
            this.Controls.Add(this.ProgTotalEnd_pb);
            this.Controls.Add(this.ProgEnd_pb);
            this.Controls.Add(this.ProgressCurrent_pb);
            this.Controls.Add(this.TotalProg_pb);
            this.Controls.Add(this.Launch_pb);
            this.Controls.Add(this.ActionLabel);
            this.Controls.Add(this.Movement_panel);
            this.DoubleBuffered = true;
            this.ForeColor = SystemColors.ControlText;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Icon = ((Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new Padding(4, 3, 4, 3);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AMain";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Launcher";
            this.TransparencyKey = Color.Black;
            this.FormClosed += new FormClosedEventHandler(this.AMain_FormClosed);
            this.Load += new System.EventHandler(this.AMain_Load);
            this.Click += new System.EventHandler(this.AMain_Click);
            this.Movement_panel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.Close_pb)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.Config_pb)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ProgTotalEnd_pb)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ProgEnd_pb)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ProgressCurrent_pb)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.TotalProg_pb)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.Launch_pb)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.Main_browser)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private Label ActionLabel;
        private Label SpeedLabel;
        public Timer InterfaceTimer;
        public PictureBox Launch_pb;
        private PictureBox Close_pb;
        private Panel Movement_panel;
        private PictureBox TotalProg_pb;
        private PictureBox ProgressCurrent_pb;
        private Label Name_label;
        private PictureBox ProgEnd_pb;
        private PictureBox ProgTotalEnd_pb;
        private Label CurrentFile_label;
        private Label CurrentPercent_label;
        private Label TotalPercent_label;
        private Label Credit_label;
        private Label Version_label;
        private PictureBox Config_pb;
        private PictureBox pictureBox1;
        private Microsoft.Web.WebView2.WinForms.WebView2 Main_browser;
    }
}

