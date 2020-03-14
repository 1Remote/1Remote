using System;
using System.Drawing;
using System.Windows.Forms;

namespace OpaqueLayer
{
    class OpaqueCommand
    {
        private OpaqueLoadingLayer _mOpaqueLoadingLayer = null;

        /// <summary>
        /// show layer
        /// </summary>
        /// <param name="control">parent</param>
        public void ShowLoadingLayer(Control control)
        {
            try
            {
                if (this._mOpaqueLoadingLayer == null)
                {
                    this._mOpaqueLoadingLayer = new OpaqueLoadingLayer(1, Color.White);
                    control.Controls.Add(this._mOpaqueLoadingLayer);
                    this._mOpaqueLoadingLayer.Dock = DockStyle.Fill;
                    this._mOpaqueLoadingLayer.BringToFront();
                }
                this._mOpaqueLoadingLayer.Enabled = true;
                this._mOpaqueLoadingLayer.Visible = true;
            }
            catch { }
        }

        public void HideOpaqueLayer()
        {
            try
            {
                if (this._mOpaqueLoadingLayer != null)
                {
                    this._mOpaqueLoadingLayer.Visible = false;
                    this._mOpaqueLoadingLayer.Enabled = false;
                }
            }
            catch (Exception ex)
            {
            }
        }

    }
}
