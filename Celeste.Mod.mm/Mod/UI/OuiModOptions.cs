﻿using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.UI {
    public class OuiModOptions : Oui {

        /// <summary>
        /// Interface used to "tag" mod options submenus.
        /// </summary>
        public interface ISubmenu { }

        public static OuiModOptions Instance;

        private TextMenu menu;

        private const float onScreenX = 960f;
        private const float offScreenX = 2880f;

        private float alpha = 0f;

        private int savedMenuIndex = -1;

        public OuiModOptions() {
            Instance = this;
        }

        public static TextMenu CreateMenu(bool inGame, EventInstance snapshot) {
            TextMenu menu = new TextMenu();

            menu.Add(new TextMenuExt.HeaderImage("menu/everest") {
                ImageColor = Color.White,
                ImageOutline = true,
                ImageScale = 0.5f
            });

            if (!inGame) {
                List<EverestModuleMetadata> missingDependencies = new List<EverestModuleMetadata>();

                lock (Everest.Loader.Delayed) {
                    if (Everest.Loader.Delayed.Count > 0) {
                        menu.Add(new TextMenuExt.SubHeaderExt(Dialog.Clean("modoptions_coremodule_notloaded_a")) { HeightExtra = 0f, TextColor = Color.OrangeRed });
                        menu.Add(new TextMenuExt.SubHeaderExt(Dialog.Clean("modoptions_coremodule_notloaded_b")) { HeightExtra = 0f, TextColor = Color.OrangeRed });

                        foreach (Tuple<EverestModuleMetadata, Action> mod in Everest.Loader.Delayed) {
                            string missingDepsString = "";
                            if (mod.Item1.Dependencies != null) {
                                // check for missing dependencies
                                List<EverestModuleMetadata> missingDependenciesForMod = mod.Item1.Dependencies
                                    .FindAll(dep => !Everest.Loader.DependencyLoaded(dep));
                                missingDependencies.AddRange(missingDependenciesForMod);

                                if (missingDependenciesForMod.Count != 0) {
                                    // format their names and versions, and join all of them in a single string
                                    missingDepsString = string.Join(", ", missingDependenciesForMod.Select(dependency => dependency.Name + " | v." + dependency.VersionString));

                                    // ensure that string is not too long, or else it would break the display
                                    if (missingDepsString.Length > 40) {
                                        missingDepsString = missingDepsString.Substring(0, 40) + "...";
                                    }

                                    // wrap that in a " ({list} not found)" message
                                    missingDepsString = $" ({missingDepsString} {Dialog.Clean("modoptions_coremodule_notloaded_notfound")})";
                                }
                            }

                            menu.Add(new TextMenuExt.SubHeaderExt(mod.Item1.Name + " | v." + mod.Item1.VersionString + missingDepsString) {
                                HeightExtra = 0f,
                                TextColor = Color.PaleVioletRed
                            });
                        }
                    }
                }

                if (Everest.Updater.HasUpdate) {
                    menu.Add(new TextMenu.Button(Dialog.Clean("modoptions_coremodule_update").Replace("((version))", Everest.Updater.Newest.Build.ToString())).Pressed(() => {
                        Everest.Updater.Update(OuiModOptions.Instance.Overworld.Goto<OuiLoggedProgress>());
                    }));
                }

                if (missingDependencies.Count != 0) {
                    menu.Add(new TextMenu.Button(Dialog.Clean("modoptions_coremodule_downloaddeps")).Pressed(() => {
                        OuiDependencyDownloader.MissingDependencies = missingDependencies;
                        OuiModOptions.Instance.Overworld.Goto<OuiDependencyDownloader>();
                    }));
                }
            }

            List<EverestModule> modules = new List<EverestModule>(Everest._Modules);
            if (Everest.Loader._ModOptionsOrder != null && Everest.Loader._ModOptionsOrder.Count > 0) {
                foreach (string modName in Everest.Loader._ModOptionsOrder) {
                    //Hack to allow user to position Everest Core in ModSettings
                    if (modName.ToLower().Equals("everest")) {
                        modules.Find(mod => mod.Metadata.Name == "Everest").CreateModMenuSection(menu, inGame, snapshot);
                        continue;
                    }
                    string modPath = Path.Combine(Everest.Loader.PathMods, modName);
                    int index = modules.Select(mod => mod.Metadata.PathDirectory).ToList<string>().IndexOf(modPath);
                    if (index != -1) {
                        modules[index].CreateModMenuSection(menu, inGame, snapshot);
                        modules.RemoveAt(index);
                    } else {
                        index = modules.Select(mod => mod.Metadata.PathArchive).ToList<string>().IndexOf(modPath);
                        if (index != -1) {
                            modules[index].CreateModMenuSection(menu, inGame, snapshot);
                            modules.RemoveAt(index);
                        }
                    }
                }
            }

            foreach (EverestModule mod in modules)
                mod.CreateModMenuSection(menu, inGame, snapshot);

            if (menu.Height > menu.ScrollableMinSize) {
                menu.Position.Y = menu.ScrollTargetY;
            }

            return menu;
        }

        private void ReloadMenu() {
            Vector2 position = Vector2.Zero;

            int selected = -1;
            if (menu != null) {
                position = menu.Position;
                selected = menu.Selection;
                Scene.Remove(menu);
            }

            menu = CreateMenu(false, null);

            if (selected >= 0) {
                menu.Selection = selected;
                menu.Position = position;
            }

            Scene.Add(menu);
        }

        public override IEnumerator Enter(Oui from) {
            ReloadMenu();

            // restore selection if coming from a submenu.
            if (savedMenuIndex != -1 && typeof(ISubmenu).IsAssignableFrom(from.GetType())) {
                menu.Selection = Math.Min(savedMenuIndex, menu.LastPossibleSelection);
                menu.Position.Y = menu.ScrollTargetY;
            }

            menu.Visible = Visible = true;
            menu.Focused = false;

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                menu.X = offScreenX + -1920f * Ease.CubeOut(p);
                alpha = Ease.CubeOut(p);
                yield return null;
            }

            menu.Focused = true;
        }

        public override IEnumerator Leave(Oui next) {
            Audio.Play(SFX.ui_main_whoosh_large_out);
            menu.Focused = false;

            // save the menu position in case we want to restore it.
            savedMenuIndex = menu.Selection;

            yield return Everest.SaveSettings();

            for (float p = 0f; p < 1f; p += Engine.DeltaTime * 4f) {
                menu.X = onScreenX + 1920f * Ease.CubeIn(p);
                alpha = 1f - Ease.CubeIn(p);
                yield return null;
            }

            menu.Visible = Visible = false;
            menu.RemoveSelf();
            menu = null;
        }

        public override void Update() {
            if (menu != null && menu.Focused &&
                Selected && Input.MenuCancel.Pressed) {
                Audio.Play(SFX.ui_main_button_back);
                Overworld.Goto<OuiMainMenu>();
            }

            base.Update();
        }

        public override void Render() {
            if (alpha > 0f)
                Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * alpha * 0.4f);
            base.Render();
        }


    }
}
