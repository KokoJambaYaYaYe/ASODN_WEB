import { defineConfig, loadEnv } from 'vite';
import plugin from '@vitejs/plugin-react';
import { federation } from '@module-federation/vite';
import packageJson from './package.json' with { type: 'json' };
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

/**
 * В ES-модулях (__dirname и __filename отсутствуют),
 * поэтому получаем их вручную через import.meta.url.
 *
 * Это необходимо, например, для корректного поиска файлов
 * сертификатов HTTPS независимо от того, из какой директории
 * была запущена команда vite.
 */
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

/**
 * defineConfig позволяет использовать подсказки типов
 * и автоматически валидировать конфигурацию Vite.
 *
 * Вместо обычного объекта используется функция, чтобы иметь
 * доступ к текущему режиму запуска (development, production и т.д.).
 */
export default defineConfig(({ mode }) => {

    /**
     * Загружаем переменные окружения из .env файлов.
     *
     * Например:
     *  development -> .env.development
     *  production  -> .env.production
     *
     * Благодаря этому не приходится менять код между окружениями —
     * достаточно изменить значения в .env.
     */
    const env = loadEnv(mode, process.cwd());

    return {

        /**
         * ==========================================================
         * ПЛАГИНЫ VITE
         * ==========================================================
         */
        plugins: [

            /**
             * Поддержка React.
             *
             * Добавляет:
             * - обработку JSX;
             * - Fast Refresh (горячее обновление компонентов);
             * - оптимизацию React во время разработки.
             */
            plugin(),

            /**
             * ======================================================
             * Module Federation
             * ======================================================
             *
             * Позволяет загружать части приложения (микрофронтенды)
             * динамически во время выполнения.
             *
             * В результате:
             *
             * Host (это приложение)
             *        │
             *        ├────────► auth_module
             *        │
             *        └────────► svod_reports
             *
             * Каждый remote собирается отдельно и может
             * деплоиться независимо.
             */
            federation({

                /**
                 * Имя текущего контейнера.
                 *
                 * Используется самим механизмом Module Federation
                 * для идентификации данного приложения.
                 */
                name: 'main-container',

                /**
                 * ==================================================
                 * Подключаемые удалённые приложения (Remotes)
                 * ==================================================
                 */
                remotes: {

                    /**
                     * Микрофронтенд со сводными отчетами.
                     */
                    svod_reports: {

                        /**
                         * Используем Native ES Modules.
                         */
                        type: 'module',

                        /**
                         * Имя удаленного контейнера.
                         * Оно должно совпадать с именем,
                         * указанным внутри remote приложения.
                         */
                        name: 'svod_reports',

                        /**
                         * Адрес remoteEntry.js.
                         *
                         * Именно этот файл содержит описание
                         * всех экспортируемых модулей remote.
                         *
                         * Пример:
                         *
                         * https://localhost:3001/remoteEntry.js
                         *
                         * Благодаря .env можно без изменения кода
                         * переключаться между dev/stage/prod.
                         */
                        entry: `${env.VITE_SVOD_REPORTS_URL}/remoteEntry.js`,
                    },

                    /**
                     * Микрофронтенд авторизации.
                     */
                    auth_module: {
                        type: 'module',
                        name: 'auth_module',
                        entry: `${env.VITE_AUTH_MODULE_URL}/remoteEntry.js`,
                    }
                },

                /**
                 * Генерацию деклараций TypeScript отключаем.
                 *
                 * Если используется чистый JavaScript
                 * или типы публикуются отдельно,
                 * эта опция не нужна.
                 */
                dts: false,

                /**
                 * ==================================================
                 * Общие зависимости (Shared)
                 * ==================================================
                 *
                 * Самая важная часть Module Federation.
                 *
                 * Без shared каждый remote загрузил бы
                 * собственную копию React, MUI и других библиотек.
                 *
                 * В результате появились бы:
                 *
                 * - Invalid hook call
                 * - разные React Context
                 * - сломанные ThemeProvider
                 * - увеличение размера приложения
                 *
                 * Shared позволяет всем приложениям
                 * использовать одну общую библиотеку.
                 */
                shared: {

                    /**
                     * React.
                     */
                    react: {

                        /**
                         * singleton означает:
                         *
                         * независимо от количества remotes
                         * в памяти будет существовать
                         * только ОДИН экземпляр React.
                         *
                         * Это обязательное условие
                         * для корректной работы хуков.
                         */
                        singleton: true,

                        /**
                         * Требуем ту же версию React,
                         * что указана в package.json хоста.
                         *
                         * Это предотвращает несовместимость
                         * между приложениями.
                         */
                        requiredVersion: packageJson.dependencies.react,
                    },

                    /**
                     * Аналогично для ReactDOM.
                     */
                    'react-dom': {
                        singleton: true,
                        requiredVersion: packageJson.dependencies['react-dom'],
                    },

                    /**
                     * Material UI.
                     *
                     * Singleton нужен потому что:
                     *
                     * - ThemeProvider общий;
                     * - Emotion Cache общий;
                     * - стили не дублируются;
                     * - Context работает корректно.
                     */
                    '@mui/material': {
                        singleton: true
                    },

                    /**
                     * Иконки MUI.
                     */
                    '@mui/icons-material': {
                        singleton: true
                    },

                    /**
                     * Emotion отвечает за генерацию CSS.
                     *
                     * Если будет несколько экземпляров,
                     * возможно:
                     *
                     * - дублирование стилей;
                     * - неправильный порядок CSS;
                     * - потеря темы.
                     */
                    '@emotion/react': {
                        singleton: true
                    },

                    '@emotion/styled': {
                        singleton: true
                    },

                    /**
                     * Fortune Sheet.
                     *
                     * Также разделяем между всеми
                     * микрофронтендами.
                     */
                    '@fortune-sheet/react': {
                        singleton: true
                    },
                },
            }),
        ],

        /**
         * ==========================================================
         * Оптимизация зависимостей
         * ==========================================================
         *
         * Во время разработки Vite предварительно
         * собирает node_modules через esbuild.
         *
         * Это ускоряет запуск dev-сервера.
         */
        optimizeDeps: {

            /**
             * Оставляем оптимизацию включенной.
             */
            disabled: false,

            /**
             * Но некоторые библиотеки исключаем.
             *
             * Причина:
             * они управляются Module Federation,
             * поэтому Vite не должен создавать
             * их собственные оптимизированные копии.
             */
            exclude: [
                'react',
                'react-dom',
                '@module-federation/vite',
                '@mui/material',
                '@mui/icons-material',
                '@emotion/react',
                '@emotion/styled',
                '@fortune-sheet/react'
            ],
        },

        /**
         * ==========================================================
         * Настройки локального сервера разработки
         * ==========================================================
         */
        server: {

            /**
             * Порт хост-приложения.
             *
             * Использование фиксированного порта удобно,
             * потому что remotes могут заранее знать,
             * где находится host.
             */
            port: 63554,

            /**
             * HTTPS.
             *
             * Используется локальный SSL-сертификат.
             *
             * Это необходимо, если:
             *
             * - remotes работают по HTTPS;
             * - используется авторизация через cookie;
             * - браузер требует Secure Context;
             * - используются некоторые Web API.
             */
            https: {

                /**
                 * Приватный ключ сертификата.
                 */
                key: fs.readFileSync(
                    path.resolve(__dirname, "./.cert/key.pem")
                ),

                /**
                 * Сам сертификат.
                 */
                cert: fs.readFileSync(
                    path.resolve(__dirname, "./.cert/cert.pem")
                ),
            },
        },

        /**
         * ==========================================================
         * Production Build
         * ==========================================================
         */
        build: {

            /**
             * Используем современный стандарт JavaScript.
             *
             * Module Federation в Vite использует:
             *
             * - Native ES Modules
             * - Dynamic import()
             * - Top-Level Await
             *
             * Поэтому target ниже ESNext
             * использовать не рекомендуется.
             */
            target: 'esnext',
        },
    };
});