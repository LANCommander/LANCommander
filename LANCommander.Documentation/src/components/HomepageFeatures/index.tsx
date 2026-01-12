import type {ReactNode} from 'react';
import clsx from 'clsx';
import Heading from '@theme/Heading';
import Link from '@docusaurus/Link';
import styles from './styles.module.css';

type FeatureItem = {
  title: string;
  description: ReactNode;
  link?: string;
};

const FeatureList: FeatureItem[] = [
  {
    title: 'Self-Hosted',
    description: (
      <>
        Run your own game distribution platform on your local network. No internet required for game installations.
        Perfect for LAN parties and closed networks. Built with ASP.NET Blazor and available for Windows, Linux, and macOS.
      </>
    ),
  },
  {
    title: 'Game Management',
    description: (
      <>
        Upload and manage game archives, manage scripts for installation and configuration, handle game keys,
        and organize your library with collections. Support for redistributables, dedicated servers, and save management.
      </>
    ),
  },
  {
    title: 'Custom Launcher',
    description: (
      <>
        Official launcher application for easy client setup. Browse your game library, install games with a single click,
        and manage your installed games. Features offline mode and automatic updates.
      </>
    ),
  },
  {
    title: 'Docker Support',
    description: (
      <>
        Pre-configured Docker container for easy deployment. Optional SteamCMD and WINE support.
        Multi-architecture support including Linux/ARM64.
      </>
    ),
  },
  {
    title: 'Scripting & SDK',
    description: (
      <>
        Powerful PowerShell scripting engine for game installation and configuration. Full SDK available for
        building custom client applications. Extensive documentation and examples.
      </>
    ),
  },
  {
    title: 'Open Source',
    description: (
      <>
        Completely open source and community-driven. View the code on{' '}
        <Link href="https://github.com/LANCommander/LANCommander">GitHub</Link>, contribute improvements,
        or join the community on <Link href="https://discord.gg/vDEEWVt8EM">Discord</Link>.
      </>
    ),
  },
];

function Feature({title, description}: FeatureItem) {
  return (
    <div className={clsx('col col--4')}>
      <div className="text--center padding-horiz--md">
        <Heading as="h3">{title}</Heading>
        <p>{description}</p>
      </div>
    </div>
  );
}

export default function HomepageFeatures(): ReactNode {
  return (
    <section className={styles.features}>
      <div className="container">
        <div className="row">
          {FeatureList.map((props, idx) => (
            <Feature key={idx} {...props} />
          ))}
        </div>
      </div>
    </section>
  );
}
