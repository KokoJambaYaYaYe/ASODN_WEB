import { defineConfig, loadEnv } from 'vite';
import plugin from '@vitejs/plugin-react';
import { federation } from '@module-federation/vite';
import packageJson from './package.json' with { type: 'json' };
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

export default defineConfig(({ mode }) => {
    const env = loadEnv(mode, process.cwd());

    return {
        plugins: [
            plugin(),

            federation({
                name: 'asodn-main-front',

                remotes: {
                    // svod_reports_module: {
                    //     type: 'module',
                    //     name: 'svod_reports_module',
                    //     // Убрали слэш перед remoteEntry.js, так как он теперь приходит из переменной
                    //     entry: `${env.VITE_SVOD_REPORTS_MODULE_URL}remoteEntry.js`,
                    // },
                    authsystem_module: {
                        type: 'module',
                        name: 'authsystem_module',
                        // Убрали слэш перед remoteEntry.js
                        entry: `${env.VITE_AUTHSYSTEM_MODULE_URL}/remoteEntry.js`,
                    }, 
                },

                dts: false,

                shared: {
                    react: {
                        singleton: true,
                        requiredVersion: packageJson.dependencies.react,
                    },

                    'react-dom': {
                        singleton: true,
                        requiredVersion: packageJson.dependencies['react-dom'],
                    },

                    '@mui/material': {
                        singleton: true,
                    },

                    '@mui/icons-material': {
                        singleton: true,
                    },

                    '@emotion/react': {
                        singleton: true,
                    },

                    '@emotion/styled': {
                        singleton: true,
                    },

                    '@fortune-sheet/react': {
                        singleton: true,
                    },
                },
            }),
        ],

        optimizeDeps: {
            disabled: false,
            exclude: [
                'react',
                'react-dom',
                '@module-federation/vite',
                '@mui/material',
                '@mui/icons-material',
                '@emotion/react',
                '@emotion/styled',
                '@fortune-sheet/react',
            ],
        },

        server: {
            port: 63554,
            https: {
                key: fs.readFileSync(path.resolve(__dirname, './.cert/key.pem')),
                cert: fs.readFileSync(path.resolve(__dirname, './.cert/cert.pem')),
            },
        },

        build: {
            target: 'esnext',
        },
    };
});