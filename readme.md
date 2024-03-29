﻿English version : https://discourse.mcneel.com/t/ctrl-shift-rmb-as-default

En attendant la version 8 et l'option `Rhino.Options.View.RotateViewAroundObjectAtMouseCursor`,
Voici un plugin qui positionne la cible de la caméra sur l'objet sous la souris afin d'interpréter le comportement `Ctrl+Shift+RMB`.


<a rel="license" href="http://creativecommons.org/licenses/by/4.0/"><img alt="Licence Creative Commons" style="border-width:0" src="https://i.creativecommons.org/l/by/4.0/88x31.png" /></a><br />Ce(tte) œuvre est mise à disposition selon les termes de la <a rel="license" href="http://creativecommons.org/licenses/by/4.0/">Licence Creative Commons Attribution 4.0 International</a>.


Installation
------------


Ce plugin est sur le serveur Yak et s'appelle `AutoCameraTarget` (La version actuelle est la 2.0.2).

![Toggle debug mode](./doc/yak.png)


Utilisation
-----------


Il n'y a qu'une seule commande: `ToggleAutoCameraTarget`.

Vous pouvez activer le ciblage automatique de la caméra lorsque Rhino démarre avec la ligne de commande dans les paramètres: `Rhino Options > General > Command Lists`


Le intersections
----------------

<table>
<tr>
    <td> <b>Intersection sur un objet solide.</b>
    <td> <img src="./doc/OnMesh.png" width="300px" />

<tr>
    <td> Ici, les arbres sont des fichiers liés en tant qu'instance de blocs. <br/>
         Mise en garde concernant les SubD, voir plus bas.
    <td> <img src="./doc/OnMesh-block.png"  width="300px" />

<tr>
    <td> <b>Intersection avec une boîte englobante.</b><br/> 
         Ici, vous avez cliqué à l'extérieur d'un objet solide.
    <td> <img src="./doc/OnBBox.png" width="300px" />

<tr>
    <td> Les courbes ne sont pas des objets solides, <br/>
    seule la boîte englobante est utilisée pour trouver l'intersection.
    <td> <img src="./doc/OnBBox-flat.png" width="300px" />

<tr>
    <td> Le programme ne cherchera pas les courbes à l'intérieur des blocs. <br/>
    La boîte englobante du bloc sera utilisée.
    <td><img src="./doc/OnBBox-block.png" width="300px" />
    
<tr>
    <td> Les intersections avec des courbes sont calculées uniquement s'il n'y a pas d'intersection avec des solides.
    <td><img src="./doc/Through.png" width="300px" />

<tr>
    <td>
        <b>Intersection avec une boîte renfermant tous les éléments visibles.</b>
    <td><img src="./doc/OnVisibleBBox.png" width="300px" />
      
<tr>
    <td> 
        <b>Intersection avec le plan médian visible.</b> <br/>
        Ici, la position du curseur sera projetée sur le plan faisant face à la caméra et médian de la boîte contenant les éléments visibles de la vue.
    <td><img src="./doc/Outside.png" width="300px" />

<tr>
    <td>
        <b>Aucune intersection possible</b> <br/>
        La dernière possibilité est lorsqu'il n'y a aucun objet visible dans la vue. <br/>
        Pour la v1, le programme ne fait rien du tout et laisse la cible de la caméra inchangée.
        Pour la v2, la caméra tourne sur elle-même.

</table>


Performance / Todo / Issues
---------------------------

Mention spéciale sur les objets SubD:
La fonction qui récupère les maillages d'objets est `RhinoObject.GetRenderMeshes`.
Son comportement est étrange avec les SubD : https://discourse.mcneel.com/t/rhinoobject-getrendermeshes-bug
Cela peut rendre l'utilisation de ce plugin désagréable lorsqu'il y a beaucoup d'objets de type SubD.

Actuellement, si un objet est partiellement visible, toute cette boîte englobante est utilisée pour le calcul de l'intersection. Pour les grands objets avec une très petite partie visible, cela peut entraîner une rotation excessive de la caméra.
peut-être est-il possible de limiter la boîte englobante de l'objet à la zone visible de l'écran...


Remarques techniques
--------------------

Navigation par défaut de Rhino:

<table>

<tr><th> 
    <th>
    <th> Shift
    <th> Ctrl
    <th> Alt

<tr>
    <td> Rotation
    <td> RMB
    <td> Lock one axis
    <td>
    <td>

<tr>
    <td> Rotation
    <td> Shift+Ctrl+RMB
    <td> Lock one axis
    <td> Lock one axis
    <td>

<tr>
    <td> Pan
    <td> Shift+RMB
    <br> Shift+Ctrl+RMB
    <td> Lock one axis
    <td> Lock one axis
    <td>

<tr>
    <td> Zoom
    <td> Ctrl+RMB
    <br> Alt+RMB
    <td> 
    <td> 
    <td>

</table>