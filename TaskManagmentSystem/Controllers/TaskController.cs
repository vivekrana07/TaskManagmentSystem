﻿using AspNetCoreHero.ToastNotification.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Reflection.Metadata;
using TaskManagmentSystem.Constants;
using TaskManagmentSystem.DTO;
using TaskManagmentSystem.Models;
using TaskManagmentSystem.Repositories;

namespace TaskManagmentSystem.Controllers
{
    public class TaskController : Controller
    {
        private readonly UserRepository _userRepository;
        private readonly TaskRepository _taskRepository;
        private readonly INotyfService _toastNotification;
        public TaskController(TaskRepository taskRepository, INotyfService toastNotification, UserRepository userRepository)
        {
            _taskRepository = taskRepository;
            _toastNotification = toastNotification;
            _userRepository = userRepository;
        }

        public IActionResult Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                _toastNotification.Warning("Session Expire, Login Again !!!");
                return View("~/Views/AuthView/Login.cshtml");
            }
            var tasklist = new TaskListDTO()
            {
                AssignedToMe = _taskRepository.GetUsersTask(userId.Value),
                TeamMatesTasks = _taskRepository.GetTeamsTask(userId.Value)
            };
            return View("~/Views/Dashboard/GenericDashboard.cshtml", tasklist);
        }

        public async Task<IActionResult> DetailView(int id)
        {
            var taskDetails = await _taskRepository.GetTaskDetailViewData(id);
            return View("~/Views/Tasks/TaskView.cshtml", taskDetails);
        }

        public async Task<IActionResult> CreateTask()
        {
            var teamViewModel = new TeamViewModel
            {
                Teams = await _taskRepository.GetListofTeams(),
                TeamMembers = new List<TeamMembers>()
            };
            return View("~/Views/Dashboard/AddTask.cshtml", teamViewModel);
        }

        public async Task<IActionResult> AddTask(IFormFile file, Tasks task)
        {
            var assingedTo = _userRepository.GetUserByUserId(task.AssignedTo);

            //fetching creator id from session
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                _toastNotification.Warning("Session Expire, Login Again !!!");
                return View("~/Views/AuthView/Login.cshtml");
            }
            var createdBy = _userRepository.GetUserByUserId(userId.Value);

            task.CreatedUser = createdBy.Username;
            task.CreatedBy = createdBy.UserId;
            task.AssignedUser = assingedTo.Username;

            task.Status = TasksStatus.ToDo;

            var result = await _taskRepository.AddTask(task);

            if (file != null && file.Length > 0)
            {
                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);

                    var document = new Attachments
                    {
                        TaskId = result.TaskId,
                        DocData = memoryStream.ToArray(),
                        DocType = file.ContentType,
                        FileName = file.FileName,
                        UploadedBy = createdBy.UserId,
                        UploadedUser = createdBy.Username,
                        UploadDate = DateTime.Now
                    };
                    await _taskRepository.AddAttachment(document);
                }
            }
            if (result != null)
            {
                _toastNotification.Success("Task Created Successfully !!");
                return RedirectToAction("Index");
            }
            else
            {
                _toastNotification.Error("Something went wrong !! Try Again");
                return View("~/Views/Dashboard/AddTask.cshtml");
            }

        }

        public async Task<IActionResult> Download(int id)
        {
            var document = await _taskRepository.GetAttachmentById(id);
            if (document != null)
            {
                return File(document.DocData, document.DocType, document.FileName);
            }
            return NotFound();
        }

        public async Task<IActionResult> AddNotes(int TaskId, string NoteText)
        {

            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                _toastNotification.Warning("Session Expire, Login Again !!!");
                return View("~/Views/AuthView/Login.cshtml");
            }
            var createdBy = _userRepository.GetUserByUserId(userId.Value);

            var noteDetail = new Notes()
            {
                NoteText = NoteText,
                TaskId = TaskId,
                CreatedUser = createdBy.Username,
                CreatedBy = createdBy.UserId,
                CreatedDate = DateTime.Now,
            };

            var result = await _taskRepository.AddNotes(noteDetail);
            if (result)
                _toastNotification.Success("Note Added Successfully !!");
            else
                _toastNotification.Error("Something went wrong !! Try Again");

            var taskDetails = await _taskRepository.GetTaskDetailViewData(TaskId);
            ModelState.Clear();
            return View("~/Views/Tasks/TaskView.cshtml", taskDetails);
        }

        [HttpGet]
        public async Task<JsonResult> GetTeamMembers(int teamId)
        {
            var teamMembers = await _taskRepository.GetListofTeamsMembers(teamId);
            return Json(teamMembers);
        }

    }
}
