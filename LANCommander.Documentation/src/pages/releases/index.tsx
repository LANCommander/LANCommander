import type {ReactNode} from 'react';
import Layout from '@theme/Layout';
import Link from '@docusaurus/Link';
import Heading from '@theme/Heading';

export default function Releases(): ReactNode {
  const releases = [
    {
      version: '2.0.0-rc1',
      title: '2.0.0 Release Candidate 1',
      path: '/releases/2.0.0-rc1',
    },
    {
      version: '1.1.5',
      title: '1.1.5',
      path: '/releases/1.1.5',
    },
    {
      version: '1.1.0',
      title: '1.1.0',
      path: '/releases/1.1.0',
    },
  ];

  return (
    <Layout title="Releases" description="LANCommander Release Notes">
      <div className="container margin-vert--lg">
        <Heading as="h1">Release Notes</Heading>
        <p>View release notes for all versions of LANCommander.</p>
        <div className="margin-vert--lg">
          {releases.map((release) => (
            <div key={release.version} className="margin-bottom--lg">
              <Heading as="h2">
                <Link to={release.path}>{release.title}</Link>
              </Heading>
            </div>
          ))}
        </div>
      </div>
    </Layout>
  );
}
