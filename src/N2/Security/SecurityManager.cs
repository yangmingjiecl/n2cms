#region License
/* Copyright (C) 2007 Cristian Libardo
 *
 * This is free software; you can redistribute it and/or modify it
 * under the terms of the GNU Lesser General Public License as
 * published by the Free Software Foundation; either version 2.1 of
 * the License, or (at your option) any later version.
 *
 * This software is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this software; if not, write to the Free
 * Software Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA
 * 02110-1301 USA, or see the FSF site: http://www.fsf.org.
 */
#endregion

using System;
using System.Security.Principal;
using System.Collections.Generic;
using System.Collections.Specialized;
using N2.Engine;

namespace N2.Security
{
	/// <summary>
	/// Manages security by subscribing to persister events and providing 
	/// methods to authorize request event.
	/// </summary>
	[Service(typeof(ISecurityManager))]
	public class SecurityManager : ISecurityManager
	{
		static string[] defaultAdministratorRoles = new[] { "Administrators" };
		static string[] defaultAdministratorUsers = new[] { "admin" };
		static string[] defaultEditorRoles = new[] { "Editors" };
		static string[] defaultWriterRoles = new[] { "Writers" };
		static string[] none = new string[0];

		private Web.IWebContext webContext;
		
		private bool enabled = true;

		public PermissionMap Administrators { get; set; }
		public PermissionMap Editors { get; set; }
		public PermissionMap Writers { get; set; }
		
		/// <summary>Creates a new instance of the security manager.</summary>
		[Obsolete("Don't use", true)]
		public SecurityManager(Web.IWebContext webContext)
		{
			this.webContext = webContext;

			Administrators = new PermissionMap(Permission.Full, defaultAdministratorRoles, defaultAdministratorUsers);
			Editors = new PermissionMap(Permission.ReadWritePublish, defaultEditorRoles, none);
			Writers = new PermissionMap(Permission.ReadWrite, defaultWriterRoles, none);
        }

        /// <summary>Creates a new instance of the security manager.</summary>
        public SecurityManager(Web.IWebContext webContext, Configuration.EditSection config)
        {
            this.webContext = webContext;

			Administrators = config.Editors.ToPermissionMap(Permission.Full, defaultAdministratorRoles, defaultAdministratorUsers);
			Editors = config.Editors.ToPermissionMap(Permission.ReadWritePublish, defaultEditorRoles, none);
			Writers = config.Editors.ToPermissionMap(Permission.ReadWrite, defaultWriterRoles, none);
        }

		#region Properties
		/// <summary>
		/// Gets user names considered as editors.
		/// </summary>
		[Obsolete("About to be gone")]
		public string[] EditorNames
		{
			get { return Editors.Users; }
		}

		/// <summary>
		/// Gets roles considered as editors.
		/// </summary>
		[Obsolete("About to be gone")]
		public string[] EditorRoles
		{
			get { return Editors.Roles; }
		}

		/// <summary>
		/// Gets roles considered as writers.
		/// </summary>
		[Obsolete("About to be gone")]
		public string[] WriterRoles
		{
			get { return Writers.Roles; }
		}

		/// <summary>
		/// Gets user names considered as administrators.
		/// </summary>
		[Obsolete("About to be gone")]
		public string[] AdminNames
		{
			get { return Administrators.Users; }
		}

		/// <summary>
		/// Gets or sets roles considered as administrators.
		/// </summary>
		[Obsolete("About to be gone")]
		public string[] AdminRoles
		{
			get { return Administrators.Roles; }
		}

		/// <summary>Check whether an item is published, i.e. it's published and isn't expired.</summary>
		/// <param name="item">The item to check.</param>
		/// <returns>A boolean indicating whether the item is published.</returns>
		public virtual bool IsPublished(ContentItem item)
		{
			return (item.Published.HasValue && DateTime.Now >= item.Published)
				&& (!item.Expires.HasValue || DateTime.Now < item.Expires.Value);
		}

		/// <summary>Gets or sets whether the security manager is enabled.</summary>
		public virtual bool Enabled
		{
			get { return enabled; }
			set { enabled = value; }
		}

		/// <summary>Gets or sets whether the security manager is enabled in the current scope. This can be used to override the security features in certain situations.</summary>
		public virtual bool ScopeEnabled
		{
			get
			{
				return !webContext.RequestItems.Contains("ItemSecurityManager.ScopeEnabled");
			}
			set
			{
				if (value && webContext.RequestItems.Contains("ItemSecurityManager.ScopeEnabled"))
					webContext.RequestItems.Remove("ItemSecurityManager.ScopeEnabled");
				else if (!value)
					webContext.RequestItems["ItemSecurityManager.ScopeEnabled"] = false;
			}
		}

		#endregion

		#region Methods
		/// <summary>Find out if a princpial has edit access.</summary>
		/// <param name="user">The princpial to check.</param>
		/// <returns>A boolean indicating whether the principal has edit access.</returns>
		public virtual bool IsEditor(IPrincipal user)
		{
			return IsAdmin(user) || Editors.Contains(user) || Writers.Contains(user);
		}

		/// <summary>Find out if a princpial has admin access.</summary>
		/// <param name="user">The princpial to check.</param>
		/// <returns>A boolean indicating whether the principal has admin access.</returns>
		public virtual bool IsAdmin(IPrincipal user)
		{
			return Administrators.Contains(user);
		}

		/// <summary>Find out if a principal is allowed to access an item.</summary>
		/// <param name="item">The item to check against.</param>
		/// <param name="user">The principal to check for allowance.</param>
		/// <returns>True if the item has public access or the principal is allowed to access it.</returns>
		public virtual bool IsAuthorized(ContentItem item, IPrincipal user)
		{
			if (!Enabled || !ScopeEnabled || IsAdmin(user))
			{
				// Disabled security manager or Editor means full access
				return true;
			}
			else if (!IsEditor(user) && !IsPublished(item))
			{
				// Non-editors cannot load unpublished items
				return false;
			}
			return item.IsAuthorized(user);
		}

		/// <summary>Find out if a principal has a certain permission for an item.</summary>
		/// <param name="item">The item to check against.</param>
		/// <param name="user">The principal to check for allowance.</param>
		/// <param name="permission">The type of permission to map against.</param>
		/// <returns>True if the item has public access or the principal is allowed to access it.</returns>
		public virtual bool IsAuthorized(IPrincipal user, ContentItem item, Permission permission)
		{
			if(permission == Permission.None)
				return true;
			if (permission == Permission.Read)
				return IsAuthorized(item, user);

			foreach (PermissionRemapAttribute remap in item.GetType().GetCustomAttributes(typeof(PermissionRemapAttribute), true))
				permission = remap.Remap(permission);

			return Administrators.Authorizes(user, item, permission)
				   || Editors.Authorizes(user, item, permission)
				   || Writers.Authorizes(user, item, permission);
		}

		[Obsolete("Use PermissionMap.IsInRoles")]
        public bool IsAuthorized(IPrincipal user, IEnumerable<string> roles)
        {
            foreach (string role in roles)
                if (user.IsInRole(role) || AuthorizedRole.Everyone.Equals(role, StringComparison.InvariantCultureIgnoreCase))
                    return true;
            return false;
        }

		#endregion
	}
}
