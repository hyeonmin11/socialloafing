using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// public interface ICommand
// {
//     void Execute(Worker worker);
// }

// public class MoveToBoxCommand : ICommand
// {
//     public Vector3 destination;
//     public MoveToBoxCommand(Vector3 destination)
//     {
//         this.destination = destination;
//     }
//     public void Execute(Worker worker)
//     {
//         worker.MoveTo(destination);
//     }
// }

// public class PickUpBoxCommand : ICommand
// {
//     public Box targetBox;
//     public PickUpBoxCommand(Box box)
//     {
//         targetBox = box;
//     }

//     public void Execute(Worker worker)
//     {
//         worker.PickUp(targetBox);
//     }
// }

// public class DropBoxCommand : ICommand
// {
//     public Vector3 dropPosition;
//     public DropBoxCommand(Vector3 dropPosition)
//     {
//         this.dropPosition = dropPosition;
//     }
//     public void Execute(Worker worker)
//     {
//         worker.DropAt(dropPosition);
//     }
// }