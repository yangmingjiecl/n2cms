using System;
using System.Security.Principal;
using N2.Plugin;
using N2.Engine;

namespace N2.Security
{
	/// <summary>
	/// Checks against unauthorized requests, and updates of content items.
	/// </summary>
	[Service(typeof(ISecurityEnforcer))]
	public class SecurityEnforcer : ISecurityEnforcer, IAutoStart
	{
		/// <summary>
		/// Is invoked when a security violation is encountered. The security 
		/// exception can be cancelled by setting the cancel property on the event 
		/// arguments.
		/// </summary>
		public event EventHandler<CancellableItemEventArgs> AuthorizationFailed;

		private Persistence.IPersister persister;
		private ISecurityManager security;
		private Web.IUrlParser urlParser;
		private Web.IWebContext webContext;

		public SecurityEnforcer(Persistence.IPersister persister, ISecurityManager security, Web.IUrlParser urlParser, Web.IWebContext webContext)
		{
			this.webContext = webContext;
			this.persister = persister;
			this.security = security;
			this.urlParser = urlParser;
		}

		#region Event Handlers
		private void ItemSavingEventHandler(object sender, CancellableItemEventArgs e)
		{
			if (security.Enabled && security.ScopeEnabled)
				OnItemSaving(e.AffectedItem);
		}

		private void ItemMovingEvenHandler(object sender, CancellableDestinationEventArgs e)
		{
			if (security.Enabled && security.ScopeEnabled)
				OnItemMoving(e.AffectedItem, e.Destination);
		}

		private void ItemDeletingEvenHandler(object sender, CancellableItemEventArgs e)
		{
			if (security.Enabled && security.ScopeEnabled)
				OnItemDeleting(e.AffectedItem);
		}

		private void ItemCopyingEvenHandler(object sender, CancellableDestinationEventArgs e)
		{
			if (security.Enabled && security.ScopeEnabled)
				OnItemCopying(e.AffectedItem, e.Destination);
		}
		#endregion

		#region Methods

		/// <summary>Checks that the current user is authorized to access the current item.</summary>
		public virtual void AuthorizeRequest()
		{
			ContentItem item = webContext.CurrentPage;
			if (item != null)
			{
				if (item != null && !security.IsAuthorized(item, webContext.User))
				{
					CancellableItemEventArgs args = new CancellableItemEventArgs(item);
					if (AuthorizationFailed != null)
						AuthorizationFailed.Invoke(this, args);

					if (!args.Cancel)
						throw new PermissionDeniedException(item, webContext.User);
				}
			}
		}

		/// <summary>Is invoked when an item is saved.</summary>
		/// <param name="item">The item that is to be saved.</param>
		protected virtual void OnItemSaving(ContentItem item)
		{
			if (!security.IsAuthorized(item, this.webContext.User))
				throw new PermissionDeniedException(item, this.webContext.User);
			IPrincipal user = this.webContext.User;
			if (user != null)
				item.SavedBy = user.Identity.Name;
			else
				item.SavedBy = null;
		}

		/// <summary>Is Invoked when an item is moved.</summary>
		/// <param name="source">The item that is to be moved.</param>
		/// <param name="destination">The destination for the item.</param>
		protected virtual void OnItemMoving(ContentItem source, ContentItem destination)
		{
			if (!security.IsAuthorized(source, this.webContext.User) || !security.IsAuthorized(destination, this.webContext.User))
				throw new PermissionDeniedException(source, this.webContext.User);
		}

		/// <summary>Is invoked when an item is to be deleted.</summary>
		/// <param name="item">The item to delete.</param>
		protected virtual void OnItemDeleting(ContentItem item)
		{
			IPrincipal user = webContext.User;
			if (!security.IsAuthorized(item, user))
				throw new PermissionDeniedException(item, user);
		}

		/// <summary>Is invoked when an item is to be copied.</summary>
		/// <param name="source">The item that is to be copied.</param>
		/// <param name="destination">The destination for the copied item.</param>
		protected virtual void OnItemCopying(ContentItem source, ContentItem destination)
		{
			if (!security.IsAuthorized(source, this.webContext.User) || !security.IsAuthorized(destination, this.webContext.User))
				throw new PermissionDeniedException(source, this.webContext.User);
		}
		#endregion

		#region IStartable Members

		public virtual void Start()
		{
			persister.ItemSaving += new EventHandler<CancellableItemEventArgs>(ItemSavingEventHandler);
			persister.ItemCopying += new EventHandler<CancellableDestinationEventArgs>(ItemCopyingEvenHandler);
			persister.ItemDeleting += new EventHandler<CancellableItemEventArgs>(ItemDeletingEvenHandler);
			persister.ItemMoving += new EventHandler<CancellableDestinationEventArgs>(ItemMovingEvenHandler);
		}

		public virtual void Stop()
		{
			persister.ItemSaving -= new EventHandler<CancellableItemEventArgs>(ItemSavingEventHandler);
			persister.ItemCopying -= new EventHandler<CancellableDestinationEventArgs>(ItemCopyingEvenHandler);
			persister.ItemDeleting -= new EventHandler<CancellableItemEventArgs>(ItemDeletingEvenHandler);
			persister.ItemMoving -= new EventHandler<CancellableDestinationEventArgs>(ItemMovingEvenHandler);
		}

		#endregion
	}
}
