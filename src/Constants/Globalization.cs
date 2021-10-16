// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Arctium.WoW.Launcher.Constants;

class Language
{
    private Dictionary<string, string> Strings;

    public Language(Dictionary<string, string> _Strings)
    {
        Strings = _Strings;
    }

    public string GetString(string a0)
    {
        return Strings[a0];
    }
}

static class Globalization
{
    private static Dictionary<string, Language> Languages = new()
    {
        {
            "en-US",
            new(new()
            {
                { "WAIT_AND_EXIT", "Closing in {0} seconds..." },
                { "OPERATING_SYSTEM", "Operating System: {0}" },
                { "MODE", "Mode: Custom Server ({0})" },
                { "ERROR_NO_CLIENT_FOUND", "[Error] No {0} client found." },
                { "ERROR_FOUND_NOT_SUPPORTED", "Your found client version {0} is not supported." },
                { "ERROR_MININUM_SUPPORTED", "The minimum required build is {0}" },
                { "STARTING_WOW_CLIENT", "Starting WoW client..." },
                { "CERTIFICATE_BUNDLE", "Certificate Bundle" },
                { "CERTIFICATE_SIGNATURE_MODULUS", "Certificate Signature Modulus" },
                { "CONNECTTO_MODULUS", "ConnectTo Modulus" },
                { "CHANGEPROTOCOL_MODULUS", "ChangeProtocol (GameCrypt) Modulus" },
                { "LOGIN_PORTAL", "Login Portal" },
                { "VERSION_URL", "Version URL" },
                { "LAUNCHER_LOGIN_REGISTRY", "Launcher Login Registry" },
                { "NOT_ALL_PATERNS_FOUND", "Not all patterns could be found:" },
                { "PLEASE_CONTACT_DEV", "Please contact the developer." },
                { "DONE", "Done :) " },
                { "DONE_2", " Done." },
                { "CAN_LOGIN", "You can login now." },
                { "ERROR_WHILE_LAUNCHING_CLIENT", "Error while launching the client." },
                { "WAITING_FOR_CLIENT_INIT", "Waiting for client initialization..." },
                { "PATCHING", "[{0}] Patching..." },
                { "PATCHING_NO_RESULT", "[{0}] No result found." },
                { "PRESS_ANY_KEY", "Press any key to continue..." },
                { "ERROR_MAPPING_PROTECTION", "Error while mapping the view with the given protection." },
                { "ERROR_VIEW_BACKUP", "Error while creating the view backup." },
                { "REFRESH_CLIENT_DATA", "Refreshing client data..." }
            })
        },
        {
            "fr-FR",
            new(new()
            {
                { "WAIT_AND_EXIT", "Fermeture dans {0} secondes..." },
                { "OPERATING_SYSTEM", "Système d'exploitation: {0}" },
                { "MODE", "Mode: Serveur personnalisé ({0})" },
                { "ERROR_NO_CLIENT_FOUND", "[Erreur] Aucun client {0} trouvé." },
                { "ERROR_FOUND_NOT_SUPPORTED", "Votre client trouvé {0} n'est pas supporté." },
                { "ERROR_MININUM_SUPPORTED", "Le build minimum requis est {0}" },
                { "STARTING_WOW_CLIENT", "Lancement du client WoW..." },
                { "CERTIFICATE_BUNDLE", "Ensemble de certificats" },
                { "CERTIFICATE_SIGNATURE_MODULUS", "Module de signature de certificat" },
                { "CONNECTTO_MODULUS", "Module ConnectTo" },
                { "CHANGEPROTOCOL_MODULUS", "Module ChangeProtocol (GameCrypt)" },
                { "LOGIN_PORTAL", "Portail de connexion" },
                { "VERSION_URL", "URL des versions" },
                { "LAUNCHER_LOGIN_REGISTRY", "Registre Launcher Login" },
                { "NOT_ALL_PATERNS_FOUND", "Tous les modèles n'ont pas pu être trouvés:" },
                { "PLEASE_CONTACT_DEV", "Veuillez contacter le développeur." },
                { "DONE", "Terminé :) " },
                { "DONE_2", " Terminé." },
                { "CAN_LOGIN", "Vous pouvez vous connecter maintenant." },
                { "ERROR_WHILE_LAUNCHING_CLIENT", "Erreur lors du lancement du client." },
                { "WAITING_FOR_CLIENT_INIT", "En attente de l'initialisation du client..." },
                { "PATCHING", "[{0}] Patch en cours..." },
                { "PATCHING_NO_RESULT", "[{0}] Aucun resultat trouvé." },
                { "PRESS_ANY_KEY", "Appuyez sur n'importe quel touche pour continuer..." },
                { "ERROR_MAPPING_PROTECTION", "Erreur lors du mappage de la vue avec la protection donnée." },
                { "ERROR_VIEW_BACKUP", "Erreur lors de la création de la sauvegarde de la vue." },
                { "REFRESH_CLIENT_DATA", "Refreshing client data..." }
            })
        },
        {
            "ru-RU",
            new(new()
            {
                { "WAIT_AND_EXIT", "Закрыть через {0} секунд..." },
                { "OPERATING_SYSTEM", "Операционная система {0}" },
                { "MODE", "Режим: Пользовательский сервер ({0})" },
                { "ERROR_NO_CLIENT_FOUND", "[Ошибка] Не найдено {0} клиентов." },
                { "ERROR_FOUND_NOT_SUPPORTED", "Найденная вами версия клиента {0} не поддерживается." },
                { "ERROR_MININUM_SUPPORTED", "Минимальная необходимая сборка {0}" },
                { "STARTING_WOW_CLIENT", "Запуск клиента WoW..." },
                { "CERTIFICATE_BUNDLE", "Комплект сертификатов" },
                { "CERTIFICATE_SIGNATURE_MODULUS", "Модуль подписи сертификата" },
                { "CONNECTTO_MODULUS", "ConnectTo Modulus" },
                { "CHANGEPROTOCOL_MODULUS", "Модуль ChangeProtocol (GameCrypt)" },
                { "LOGIN_PORTAL", "Портал входа в систему" },
                { "VERSION_URL", "URL версии" },
                { "LAUNCHER_LOGIN_REGISTRY", "Реестр входа в программу запуска" },
                { "NOT_ALL_PATERNS_FOUND", "Не все шаблоны можно найти:" },
                { "PLEASE_CONTACT_DEV", "Обратитесь к разработчику." },
                { "DONE", "Выполнено :) " },
                { "DONE_2", " Выполнено." },
                { "CAN_LOGIN", "Вы можете войти сейчас." },
                { "ERROR_WHILE_LAUNCHING_CLIENT", "Ошибка при запуске клиента." },
                { "WAITING_FOR_CLIENT_INIT", "Ожидание инициализации клиента..." },
                { "PATCHING", "[{0}] Патчинг..." },
                { "PATCHING_NO_RESULT", "[{0}] No Результатов не найдено found." },
                { "PRESS_ANY_KEY", "нажмите любую клавишу чтобы продолжить..." },
                { "ERROR_MAPPING_PROTECTION", "Ошибка при сопоставлении представления с данной защитой." },
                { "ERROR_VIEW_BACKUP", "Ошибка при создании резервной копии представления." },
                { "REFRESH_CLIENT_DATA", "Refreshing client data..." }
            })
        },
    };
    
    public static string GetString(string a0, string a1 = null)
    {
        if (!string.IsNullOrEmpty(a1))
            return Languages[a1].GetString(a0);
        else
        {
            string a2 = CultureInfo.CurrentCulture.Name;
            string a3 = Languages[a2].GetString(a0);

            if (string.IsNullOrEmpty(a3))
                return Languages["en-US"].GetString(a0);
            else
                return a3;
        }
    }
}