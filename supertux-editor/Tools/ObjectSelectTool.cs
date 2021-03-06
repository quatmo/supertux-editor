//  SuperTux Editor
//  Copyright (C) 2006 Matthias Braun <matze@braunis.de>
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using OpenGl;
using DataStructures;
using SceneGraph;
using Gtk;
using Gdk;
using Undo;

public sealed class ObjectSelectTool : ObjectToolBase, ITool, IDisposable
{
	private IObject activeObject;	//This can be also called like "drag master object"
	private Vector pressPoint;
	private Vector mousePoint;	//used when drawing selections
	private Vector originalPosition;
	private enum State {
		NONE,
		DRAGGING,
		SELECTING
	};
	private State state;
	private List<ControlPoint> controlPoints = new List<ControlPoint>();
	private List<IObject> selectedObjects = new List<IObject>();

	public event RedrawEventHandler Redraw;

	public ObjectSelectTool(Application application, Sector sector)
	{
		this.application = application;
		this.sector = sector;
		if (sector != null) sector.ObjectRemoved += OnObjectRemoved;
	}

	public void Dispose()
	{
		if (sector != null) sector.ObjectRemoved -= OnObjectRemoved;
	}

	public void Draw(Gdk.Rectangle cliprect)
	{
		if (state == State.SELECTING) {
			// FIXME: We shouldn't have to mess around here with raw OpenGL
			gl.Disable(gl.TEXTURE_2D);

			// Draw the background box
			gl.Color4f(0, 0.7f, 1, 0.5f);
			gl.Begin(gl.QUADS);
			gl.Vertex2f(pressPoint.X, pressPoint.Y);
			gl.Vertex2f(mousePoint.X, pressPoint.Y);
			gl.Vertex2f(mousePoint.X, mousePoint.Y);
			gl.Vertex2f(pressPoint.X, mousePoint.Y);
			gl.End();

			// Draw an outline around it
			gl.Color4f(0.5f, 1.0f, 1, 1.0f);
			gl.Begin(gl.LINE_LOOP);
			gl.Vertex2f(pressPoint.X, pressPoint.Y);
			gl.Vertex2f(mousePoint.X, pressPoint.Y);
			gl.Vertex2f(mousePoint.X, mousePoint.Y);
			gl.Vertex2f(pressPoint.X, mousePoint.Y);
			gl.End();

			gl.Enable(gl.TEXTURE_2D);
			gl.Color4f(1, 1, 1, 1);
		}

		foreach(IObject selectedObject in selectedObjects) {
			IObject obj = selectedObject;
			if(obj is ControlPoint)
				obj = ((ControlPoint) obj).Object;

			gl.Color4f(1, 0, 0, 0.7f);
			obj.GetSceneGraphNode().Draw(cliprect);
			gl.Color4f(1, 1, 1, 1);
		}
		foreach(ControlPoint point in controlPoints) {
			point.Draw(cliprect);
		}
	}

	public void OnMouseButtonPress(Vector mousePos, int button, ModifierType Modifiers)
	{
		if (button == 1) {
			IObject obj = FindObjectAt(mousePos);
			Application.EditorApplication.CurrentRenderer.GrabFocus(); //HACK: remove focus from PropertiesView NOW so it can save things.
			
			if (obj == null)
			{
				// Start drawing the select rectangle
				state = State.SELECTING;
				pressPoint = mousePos;
				mousePoint = mousePos;
			}
			else
			{
				if ((Modifiers & ModifierType.ControlMask) != 0)
				{
					if (selectedObjects.Contains(obj))
					{
						selectedObjects.Remove(obj);
					}
					else
					{
						selectedObjects.Add(obj);
					}
				}
				else
				{
					if (!selectedObjects.Contains(obj))
					{
						selectedObjects.Clear();
						selectedObjects.Add(obj);
					}

					// Start moving the selected objects around
					state = State.DRAGGING;
					pressPoint = mousePos;
					originalPosition = new Vector(obj.Area.Left, obj.Area.Top);
					MakeActive(obj);
				}
			}
		}
		else if (button == 3)
		{
			PopupMenu(button);
		}
		Redraw();

		/*if (false)
		{
			if (state == State.SELECTING) {
				state = State.NONE;
			} else {
				if ((Modifiers & ModifierType.ControlMask) != 0) {	//CTRL+L click => add/remove clicked object from selection
					IObject clickedObject = FindObjectAt(mousePos);
					if (clickedObject != null && clickedObject is IGameObject)
						if (selectedObjects.Contains(clickedObject))
							selectedObjects.Remove(clickedObject);
						else
							selectedObjects.Add(clickedObject);

					if (selectedObjects.Count == 1)	{		//show properties
						MakeActive(selectedObjects[0]);
						//application.EditProperties(selectedObjects[0], selectedObjects[0].GetType().Name);
					} else {
						activeObject = null;
						controlPoints.Clear();
						application.EditProperties(selectedObjects, "Group of " + selectedObjects.Count.ToString() + " objects");
					}
				} else {						//L click => try to locate dragged/selected object
					if (activeObject == null || !activeObject.Area.Contains(mousePos)) {
						activeObject = null;
						foreach (IObject selectedObject in selectedObjects) {	//try to find "master object" for multi-dragging
							if (selectedObject.Area.Contains(mousePos)) {
								activeObject = selectedObject;
								break;
							}
						}
						if (activeObject == null)
							MakeActive(FindObjectAt(mousePos));
					}
				}

				if(activeObject != null && button == 1) {		//start drag if we have activeObject
					pressPoint = mousePos;
					originalPosition = new Vector(activeObject.Area.Left, activeObject.Area.Top);
					state = State.DRAGGING;
				}
			}
			Redraw();

			if (button == 3) {
				if (state == State.DRAGGING) {				//both buttons => drag canceled => calculate current delta and shift all objects back
					Vector shift = originalPosition - new Vector (activeObject.Area.Left, activeObject.Area.Top);
					foreach (IObject selectedObject in selectedObjects) { 	//Shift area for all other objects in list
						RectangleF Area = selectedObject.Area;
						Area.Move(shift);
						selectedObject.ChangeArea(Area);
					}
					state = State.NONE;
				} else {							//R click => initiate drag-select
					pressPoint = mousePos;
					mousePoint = mousePos;
					state = State.SELECTING;
				}
			}
		}*/
	}

	public void OnMouseButtonRelease(Vector mousePos, int button, ModifierType Modifiers)
	{
		if (button == 1) {
			switch(state)
			{
			case State.NONE:
				break;

			case State.SELECTING:
				// Add objects in the selected area to selectedObjects

                                // Flush selected objects if it's not CTRL+select
				if ((Modifiers & ModifierType.ControlMask) == 0)
				{
					selectedObjects.Clear();
				}

				RectangleF selectedArea = new RectangleF(pressPoint, mousePos);
				foreach(IObject Object in sector.GetObjects(typeof(IObject)))
				{
					if (selectedArea.Contains(Object.Area))
					{
						if (selectedObjects.Contains(Object))
						{
							selectedObjects.Remove(Object);
						}
						else
						{
							selectedObjects.Add(Object);
						}
					}
				}

				if (selectedObjects.Count == 1)
				{
					//show properties
					MakeActive(selectedObjects[0]);
					//application.EditProperties(selectedObjects[0], selectedObjects[0].GetType().Name);
				}
				else
				{
					// resizing is unavailable if more objects are selected
					activeObject = null;
					controlPoints.Clear();
					application.EditProperties(selectedObjects, "Group of " + selectedObjects.Count.ToString() + " objects");
				}
				break;

			case State.DRAGGING:
				// Finish dragging and register undo command
				Vector newPosition = new Vector (activeObject.Area.Left, activeObject.Area.Top);//Area is up to date, no need to calculate it again
				if (newPosition != originalPosition) // FIXME: is that ok in C# or do we compare the reference here?
				{
					Vector totalShift = newPosition - originalPosition;
					MultiCommand multi_command = new MultiCommand("Moved " + selectedObjects.Count + " objects");

					foreach (IObject selectedObject in selectedObjects)
					{
						// copy new area to variable
						RectangleF oldArea = selectedObject.Area;

						// and shift it to it's oreginal location
						oldArea.Move(-totalShift);

						multi_command.Add(new ObjectAreaChangeCommand(
									  "Moved Object " + selectedObject,
									  oldArea,
									  selectedObject.Area, // We are already on new area
									  selectedObject));
					}

					UndoManager.AddCommand(multi_command);
				}

				break;
			}

			state = State.NONE;
			Redraw();
		}

		/*if (false)
		{
			if (button == 1 && state == State.DRAGGING) {
				state = State.NONE;

				Vector newPosition = new Vector (activeObject.Area.Left, activeObject.Area.Top);//Area is up to date, no need to calculate it again
				if (originalPosition != newPosition) {
					Command command = null;
					List<Command> commandList = new List<Command>();
					Vector totalShift = newPosition - originalPosition;
					foreach (IObject selectedObject in selectedObjects) {
						RectangleF oldArea = selectedObject.Area;			//copy new area to variable
						oldArea.Move(-totalShift);					//	and shift it to it's oreginal location
						command = new ObjectAreaChangeCommand(
							"Moved Object " + selectedObject,
							oldArea,
							selectedObject.Area,					//We are already on new area
							selectedObject);
						commandList.Add(command);
					}

					if (commandList.Count > 1)			//If there are more items, then create multiCommand, otherwise keep single one
						command = new MultiCommand("Moved " + commandList.Count.ToString() + " objects", commandList);

					UndoManager.AddCommand(command);		//All commands are already done.. no need to do that again

					Redraw();
				} else {						//L click + no CTRL => select single object under cursor
					if ((Modifiers & ModifierType.ControlMask) == 0) {
						MakeActive(FindObjectAt(mousePos));
						Redraw();
					}
				}
			}
			if (button == 3 && state == State.SELECTING) {
				state = State.NONE;
				if ((pressPoint - mousePos).Norm() < 10) {		//for moves within 10px circle around pressPoint show popup menu
					bool hit = false;
					foreach (IObject selectedObject in selectedObjects) {
						hit |= selectedObject.Area.Contains(mousePos);
					}
					if (!hit) {
						MakeActive(FindObjectAt(mousePos));
						Redraw();
					}
					if (hit || activeObject != null)		//Show popup menu when clicked on object (from selection or new one)
						PopupMenu(button);
				} else {
					if ((Modifiers & ModifierType.ControlMask) == 0)	//Flush selected objects if it's not CTRL+select
						selectedObjects.Clear();

					RectangleF selectedArea = new RectangleF(pressPoint, mousePos);
					foreach(IObject Object in sector.GetObjects(typeof(IObject))) {
						if (selectedArea.Contains(Object.Area)) {
							if (selectedObjects.Contains(Object))
								selectedObjects.Remove(Object);
							else
								selectedObjects.Add(Object);
						}
					}
					if (selectedObjects.Count == 1)	{		//show properties
						MakeActive(selectedObjects[0]);
						//application.EditProperties(selectedObjects[0], selectedObjects[0].GetType().Name);
					} else {
						activeObject = null;					//resizing is unavailable if more objects are selected
						controlPoints.Clear();
						application.EditProperties(selectedObjects, "Group of " + selectedObjects.Count.ToString() + " objects");
					}
					Redraw();
				}
			}
		}*/
	}

	public void OnMouseMotion(Vector mousePos, ModifierType Modifiers)
	{
		switch(state)
		{
		case State.NONE:
			break;

		case State.DRAGGING:
			moveObjects(mousePos, SnapValue(Modifiers));
			break;

		case State.SELECTING:
			// store mouse position for drawin routine
			mousePoint = mousePos;
			break;
		}
		Redraw();
	}

	private void moveObjects(Vector mousePos, int snap)
	{
		Vector newPosition = getNewPosition(mousePos, snap);
		Vector oldPosition = new Vector (activeObject.Area.Left, activeObject.Area.Top);
		Vector shift = newPosition - oldPosition;				//calculate difference between current and new position

		foreach (IObject selectedObject in selectedObjects) {			//	and apply it to all objects in selection
			RectangleF Area = selectedObject.Area;
			Area.Move(shift);
			selectedObject.ChangeArea(Area);
		}

		Redraw();
	}

	private IObject FindObjectAt(Vector pos)
	{
		foreach(ControlPoint point in controlPoints) {
			if(point.Area.Contains(pos)) {
				return point;
			}
		}

		IObject firstObject = null;
		bool foundLastActive = false;

		// Cycle through the objects which share a point
		foreach(IObject Object in sector.GetObjects(typeof(IObject))) {
			if(Object.Area.Contains(pos)) {
				if(firstObject == null)
					firstObject = Object;

				if(Object == activeObject) {
					foundLastActive = true;
					continue;
				}

				if(foundLastActive) {
					return Object;
				}
			}
		}

		if(firstObject != null)
			return firstObject;

		return null;
	}

	public void MakeActive(IObject Object)
	{
		if (activeObject != Object) {		//ignore MakeActive(activeObject)
			activeObject = Object;

			if(! (activeObject is ControlPoint)) {
				if(activeObject != null)
					application.EditProperties(activeObject, activeObject.GetType().Name);
				controlPoints.Clear();
			}

			if(activeObject != null && activeObject.Resizable) {
				controlPoints.Add(new ControlPoint(activeObject,
					                           ControlPoint.AttachPoint.TOP | ControlPoint.AttachPoint.LEFT));
				controlPoints.Add(new ControlPoint(activeObject,
					                           ControlPoint.AttachPoint.TOP));
				controlPoints.Add(new ControlPoint(activeObject,
					                           ControlPoint.AttachPoint.TOP | ControlPoint.AttachPoint.RIGHT));
				controlPoints.Add(new ControlPoint(activeObject,
					                           ControlPoint.AttachPoint.LEFT));
				controlPoints.Add(new ControlPoint(activeObject,
					                           ControlPoint.AttachPoint.RIGHT));
				controlPoints.Add(new ControlPoint(activeObject,
					                           ControlPoint.AttachPoint.BOTTOM | ControlPoint.AttachPoint.LEFT));
				controlPoints.Add(new ControlPoint(activeObject,
					                           ControlPoint.AttachPoint.BOTTOM));
				controlPoints.Add(new ControlPoint(activeObject,
					                           ControlPoint.AttachPoint.BOTTOM | ControlPoint.AttachPoint.RIGHT));
			}
			LogManager.Log(LogLevel.Debug, "ObjectsEditor:MakeActive(" + activeObject + ")");
		}

		//selectedObjects.Clear();
		//if (Object != null)
		//selectedObjects.Add(Object);
	}

	private void OnObjectRemoved(Sector sector, IGameObject Object) {			//Remove "red shadow" when active object is removed by Undo/Redo
		if (activeObject==Object) {
			activeObject = null;
		}
		if (selectedObjects.Contains(Object as IObject)) {
			selectedObjects.Remove(Object as IObject);
			Redraw();
		}
	}

	private void PopupMenu(int button)
	{
		if(selectedObjects.Count == 0)
			return;

		if(activeObject is ControlPoint)
			return;

		bool groupCloneable = true;	//foreach cycle to get required statistics
		foreach (IObject selectedObject in selectedObjects) {
			groupCloneable &= selectedObject is ICloneable;
		}

		Menu popupMenu = new Menu();

		MenuItem cloneItem = new MenuItem("Clone");
		cloneItem.Activated += OnClone;
		cloneItem.Sensitive = groupCloneable;
		popupMenu.Append(cloneItem);

		if(selectedObjects.Count == 1 && selectedObjects[0] is IPathObject) {
			IPathObject pathObject = (IPathObject) selectedObjects[0];

			MenuItem editPathItem = new MenuItem("Edit Path");
			editPathItem.Activated += OnEditPath;
			popupMenu.Append(editPathItem);

			if (pathObject.PathRemovable) {
				MenuItem deletePathItem = new MenuItem("Delete Path");
				deletePathItem.Sensitive = pathObject.Path != null;
				deletePathItem.Activated += OnDeletePath;
				popupMenu.Append(deletePathItem);
			}
		}

		MenuItem deleteItem = new ImageMenuItem(Stock.Delete, null);
		deleteItem.Activated += OnDelete;
		popupMenu.Append(deleteItem);

		popupMenu.ShowAll();
		popupMenu.Popup();
	}

	private void OnClone(object o, EventArgs args)
	{
		List<Command> commands = new List<Command>();
		Command command = null;
		foreach (IGameObject selectedObject in selectedObjects)
			try {
				object newObject = ((ICloneable) selectedObject).Clone();
				IGameObject gameObject = (IGameObject) newObject;
				sector.Add(gameObject, gameObject.GetType().Name + " (clone)", ref command);
				commands.Add(command);
			} catch(Exception e) {
				ErrorDialog.Exception(e);
			}
		if (commands.Count > 1)						//If there are more items, then create multiCommand, otherwise keep single one
			command = new MultiCommand("Cloned " + commands.Count.ToString() + " objects", commands);
		UndoManager.AddCommand(command);
	}

	private void OnEditPath(object o, EventArgs args)
	{
		application.SetToolPath();					//iPathToEdit is set when calling "EditProperties()" and already contains "selectedObjects[0]"
	}

	private void OnDeletePath(object o, EventArgs args)
	{
		IPathObject pathObject = (IPathObject) selectedObjects[0];
		if (pathObject.Path != null) {
			Command command = new PropertyChangeCommand("Removed path from " + selectedObjects[0],
								    LispReader.FieldOrProperty.Lookup(typeof(IPathObject).GetProperty("Path")),
								    selectedObjects[0],
								    null);
			command.Do();
			UndoManager.AddCommand(command);
		}
	}

	private void OnDelete(object o, EventArgs args)
	{
		List<IObject> Objects = new List<IObject>(selectedObjects);	//OnObjectRemoved() tries to access selectedObject and that's not possible during "foreach"
		List<Command> commands = new List<Command>();
		Command command = null;
		foreach (IGameObject selectedObject in Objects) {
			sector.Remove(selectedObject, ref command);
			commands.Add(command);
		}
		if (commands.Count > 1)						//If there are more items, then create multiCommand, otherwise keep single one
			command = new MultiCommand("Deleted " + commands.Count.ToString() + " objects", commands);
		UndoManager.AddCommand(command);
	}

	private Vector getNewPosition(Vector mousePos, int snap) {
		Vector spos = originalPosition;
		spos += mousePos - pressPoint;
		if (snap > 0) {
			spos.X += snap / 2;					//PressPoint would be cutting edge instead of "center" without this correction
			spos.Y += snap / 2;
			// TODO: Get this right for area objects, they currently snap to the
			//       handle instead of the actual object...
			spos = new Vector((float) ((int) spos.X / snap) * snap - ((spos.X<0)?snap:0),
			                  (float) ((int) spos.Y / snap) * snap - ((spos.Y<0)?snap:0));
		}

		return spos;
	}
}

/* EOF */
