using System.ComponentModel.DataAnnotations;
using AppMvc.SeidoHelpers;
using Microsoft.AspNetCore.Mvc;
using Models.DTO;

namespace AppMvc.ViewModels
{
	public class DataSourceInfo
    {
        public GstUsrInfoAllDto Info { get; set; }
    }
}